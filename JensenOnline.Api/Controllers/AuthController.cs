using JensenOnline.Api.Core.Interfaces;
using JensenOnline.Api.Core.Services;
using JensenOnline.Api.Data;
using JensenOnline.Api.Data.Entities;
using JensenOnline.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JensenOnline.Api.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    // Samma meddelande oavsett om e-post eller lösenord var fel, eller om kontot är låst,
    // så att en angripare inte kan ta reda på vilka konton som finns (T1)
    private const string LoginFailedMessage = "Fel e-post eller lösenord.";

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _audit; // NYTT

    //Här sätts DI upp för controllern
    public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager,
        ITokenService tokenService, IAuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _audit = audit; // NYTT
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email };
        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            // NYTT: logga misslyckad registrering (aldrig lösenordet)
            await _audit.LogAsync(AuditService.Actions.Register, false, "Registrering misslyckades", email: dto.Email);

            // Visa fel på lösenordet, men avslöja inte om e-posten redan finns
            var errors = result.Errors.Where(e => e.Code.StartsWith("Password")).Select(e => e.Description).ToArray();
            if (errors.Length > 0)
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Password"] = errors }));

            return Problem(title: "Registreringen kunde inte genomföras.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Rollen sätts ALLTID här på servern, aldrig från klienten (T9)
        await _userManager.AddToRoleAsync(user, DbSeeder.CustomerRole);

        // NYTT: logga lyckad registrering
        await _audit.LogAsync(AuditService.Actions.Register, true, userId: user.Id, email: user.Email);

        return StatusCode(StatusCodes.Status201Created, new { message = "Kontot har skapats." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // 1. Finns användaren i systemet?
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
        {
            // NYTT: hasha lösenordet ändå, så att svaret tar lika lång tid som när kontot finns.
            // Annars kan en angripare se på svarstiden vilka e-postadresser som är registrerade (T1)
            _userManager.PasswordHasher.HashPassword(new AppUser(), dto.Password);

            await _audit.LogAsync(AuditService.Actions.LoginFailed, false, "Okänt konto", email: dto.Email);
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        // 2. Kontrollera lösenordet. lockoutOnFailure: true räknar misslyckade försök
        //    och låser kontot efter 5 försök (account lockout, T1)
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            // NYTT: kontot är låst. Användaren får samma svar som vid fel lösenord, men i loggen syns skillnaden
            await _audit.LogAsync(AuditService.Actions.LoginLockedOut, false, userId: user.Id, email: user.Email);
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!result.Succeeded)
        {
            // NYTT: fel lösenord
            await _audit.LogAsync(AuditService.Actions.LoginFailed, false, "Fel lösenord", userId: user.Id, email: user.Email);
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        // 3. Skapa token och lägg den i en säker cookie
        var token = await _tokenService.CreateTokenAsync(user);

        Response.Cookies.Append(TokenService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,                 // JavaScript kan inte läsa token (skydd mot XSS, T4)
            Secure = true,                   // skickas bara över HTTPS
            SameSite = SameSiteMode.Strict,  // skickas inte från andra sajter (skydd mot CSRF)
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenService.LifetimeMinutes)
        });

        // NYTT: logga lyckad inloggning
        await _audit.LogAsync(AuditService.Actions.LoginSuccess, true, userId: user.Id, email: user.Email);

        // Token skickas INTE i svaret, bara det frontend behöver för att visa rätt meny
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserInfoDto(user.Id, user.Email!, roles));
    }

    [AllowAnonymous] // utloggning ska fungera även om token har gått ut
    [HttpPost("logout")]
    public async Task<IActionResult> Logout() // NYTT: async, eftersom vi loggar
    {
        // NYTT: logga vem som loggade ut (om token fortfarande var giltig)
        if (User.Identity?.IsAuthenticated == true)
            await _audit.LogAsync(AuditService.Actions.Logout, true);

        // En HttpOnly-cookie kan bara tas bort av servern
        Response.Cookies.Delete(TokenService.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
        return NoContent();
    }

    // Vem är inloggad? Frontend kan inte läsa cookien, så den frågar servern.
    [HttpGet("me")]
    public IActionResult Me()
    {
        var roles = User.FindAll("role").Select(c => c.Value).ToList();
        return Ok(new UserInfoDto(User.GetUserId()!, User.GetEmail() ?? "", roles));
    }
}