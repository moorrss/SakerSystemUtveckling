using JensenOnline.Api.Data.Entities;

namespace JensenOnline.Api.Core.Interfaces;

//Interfacet beskriver vad tjänsten kan göra. Controllers beror bara på detta, inte på själva klassen
public interface ITokenService
{
    int LifetimeMinutes { get; }

    Task<string> CreateTokenAsync(AppUser user);
}