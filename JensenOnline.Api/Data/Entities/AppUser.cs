using Microsoft.AspNetCore.Identity;

namespace JensenOnline.Api.Data.Entities;
public class AppUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}