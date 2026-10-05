using JensenOnline.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JensenOnline.Api.Data;

public static class DbSeeder
{
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment env)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        await db.Database.EnsureCreatedAsync();

        // Rollerna Admin och Customer
        foreach (var role in new[] { AdminRole, CustomerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Testkonton skapas BARA i utvecklingsmiljön.
        // Admins kan aldrig skapas via API:t, bara här eller av en befintlig admin.
        if (env.IsDevelopment())
        {
            await CreateUserAsync(userManager, "admin@jensenonline.se", "Admin-Demo-2026!", AdminRole);
            await CreateUserAsync(userManager, "kund@jensenonline.se", "Kund-Demo-2026!", CustomerRole);
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product { Name = "Laptop Pro 14", Description = "14-tums laptop med 16 GB RAM.", Price = 12999m, Stock = 15 },
                new Product { Name = "Trådlös mus", Description = "Ergonomisk mus med tyst klick.", Price = 349m, Stock = 120 },
                new Product { Name = "Mekaniskt tangentbord", Description = "Nordisk layout.", Price = 1199m, Stock = 40 },
                new Product { Name = "27-tums skärm", Description = "IPS-panel, 1440p.", Price = 3490m, Stock = 25 },
                new Product { Name = "USB-C docka", Description = "HDMI, USB-A och Ethernet.", Price = 1590m, Stock = 60 }
            );
            await db.SaveChangesAsync();
        }
    }

    private static async Task CreateUserAsync(UserManager<AppUser> userManager, string email, string password, string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null) return;

        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
    }
}