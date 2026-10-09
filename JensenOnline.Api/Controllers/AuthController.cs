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
   
    private const string LoginFailedMessage = "Fel e-post eller lösenord.";

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _audit; 

  
    public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager,
        ITokenService tokenService, IAuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _audit = audit; 
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
           
            await _audit.LogAsync(AuditService.Actions.Register, false, "Registrering misslyckades", email: dto.Email);

            var errors = result.Errors.Where(e => e.Code.StartsWith("Password")).Select(e => e.Description).ToArray();
            if (errors.Length > 0)
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Password"] = errors }));

            return Problem(title: "Registreringen kunde inte genomföras.", statusCode: StatusCodes.Status400BadRequest);
        }

  
        await _userManager.AddToRoleAsync(user, DbSeeder.CustomerRole);

        await _audit.LogAsync(AuditService.Actions.Register, true, userId: user.Id, email: user.Email);

        return StatusCode(StatusCodes.Status201Created, new { message = "Kontot har skapats." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
      
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
        {
        
            _userManager.PasswordHasher.HashPassword(new AppUser(), dto.Password);

            await _audit.LogAsync(AuditService.Actions.LoginFailed, false, "Okänt konto", email: dto.Email);
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

    
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
           
            await _audit.LogAsync(AuditService.Actions.LoginLockedOut, false, userId: user.Id, email: user.Email);
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!result.Succeeded)
        {
           
            await _audit.LogAsync(AuditService.Actions.LoginFailed, false, "Fel lösenord", userId: user.Id, email: user.Email);
            return Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        
        var token = await _tokenService.CreateTokenAsync(user);

        Response.Cookies.Append(TokenService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,                
            Secure = true,                  
            SameSite = SameSiteMode.Strict,  
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenService.LifetimeMinutes)
        });

        
        await _audit.LogAsync(AuditService.Actions.LoginSuccess, true, userId: user.Id, email: user.Email);

       
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserInfoDto(user.Id, user.Email!, roles));
    }

    [AllowAnonymous] 
    [HttpPost("logout")]
    public async Task<IActionResult> Logout() 
    {
       
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


    [HttpGet("me")]
    public IActionResult Me()
    {
        var roles = User.FindAll("role").Select(c => c.Value).ToList();
        return Ok(new UserInfoDto(User.GetUserId()!, User.GetEmail() ?? "", roles));
    }
}