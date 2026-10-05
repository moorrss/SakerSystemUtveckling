using JensenOnline.Api.Data;
using JensenOnline.Api.Dtos;
using JensenOnline.Api.Models;
using JensenOnline.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JensenOnline.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // Samma meddelande oavsett om e-post eller lösenord var fel, eller om kontot är låst,
    // så att en angripare inte kan ta reda på vilka konton som finns (T1)
    private const string LoginFailedMessage = "Fel e-post eller lösenord.";

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly TokenService _tokenService;

    public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, TokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email };
        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            // Visa fel på lösenordet, men avslöja inte om e-posten redan finns
            var errors = result.Errors.Where(e => e.Code.StartsWith("Password")).Select(e => e.Description).ToArray();
            if (errors.Length > 0)
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Password"] = errors }));

            return Problem(title: "Registreringen kunde inte genomföras.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Rollen sätts ALLTID här på servern, aldrig från klienten (T9)
        await _userManager.AddToRoleAsync(user, DbSeeder.CustomerRole);

        return StatusCode(StatusCodes.Status201Created, new { message = "Kontot har skapats." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);

        // lockoutOnFailure: true räknar misslyckade försök och låser kontot (account lockout, T1)
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);

        var token = await _tokenService.CreateTokenAsync(user);

        Response.Cookies.Append(TokenService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,                 // JavaScript kan inte läsa token (skydd mot XSS, T4)
            Secure = true,                   // skickas bara över HTTPS
            SameSite = SameSiteMode.Strict,  // skickas inte från andra sajter (skydd mot CSRF)
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenService.LifetimeMinutes)
        });

        // Token skickas INTE i svaret, bara det frontend behöver för att visa rätt meny
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserInfoDto(user.Id, user.Email!, roles));
    }

    [HttpPost("logout")]
    [AllowAnonymous] // utloggning ska fungera även om token har gått ut
    public IActionResult Logout()
    {
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