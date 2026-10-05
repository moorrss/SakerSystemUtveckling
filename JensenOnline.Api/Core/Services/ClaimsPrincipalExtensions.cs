using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace JensenOnline.Api.Core.Services;

public static class ClaimsPrincipalExtensions
{
    // Användar-ID hämtas ALLTID från den signerade token,
    // aldrig från URL:en eller request-bodyn (T6)
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Sub);

    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Email);
}