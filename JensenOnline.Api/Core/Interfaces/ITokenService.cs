using JensenOnline.Api.Data.Entities;

namespace JensenOnline.Api.Core.Interfaces;

public interface ITokenService
{
    int LifetimeMinutes { get; }

    Task<string> CreateTokenAsync(AppUser user);
}