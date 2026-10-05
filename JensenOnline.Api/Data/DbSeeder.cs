using JensenOnline.Api.Models;
using JensenOnline.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JensenOnline.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Skapar databasen om den inte finns. I produktion används migrationer.
        await db.Database.EnsureCreatedAsync();

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
}