using System.Security.Claims;
using JensenOnline.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JensenOnline.Api.Core.Interfaces;

namespace JensenOnline.Api.Core.Services;

public class JwtSettings
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public int LifetimeMinutes { get; init; } = 30;
    public SymmetricSecurityKey SigningKey { get; init; } = null!;
}

public class TokenService : ITokenService
{
    public const string CookieName = "access_token";

    private readonly JwtSettings _settings;
    private readonly UserManager<AppUser> _userManager;

    public TokenService(JwtSettings settings, UserManager<AppUser> userManager)
    {
        _settings = settings;
        _userManager = userManager;
    }

    public int LifetimeMinutes => _settings.LifetimeMinutes;
    public async Task<string> CreateTokenAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in await _userManager.GetRolesAsync(user))
            claims.Add(new Claim("role", role));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_settings.LifetimeMinutes),
            SigningCredentials = new SigningCredentials(_settings.SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}