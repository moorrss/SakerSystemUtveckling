using JensenOnline.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace JensenOnline.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous] // Alla får läsa produkter, även icke-inloggade 
    public async Task<IActionResult> GetProducts()
    {
        // Select gör att bara de fält vi väljer skickas ut (inte hela databasentiteten)
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Description, p.Price, p.Stock })
            .ToListAsync();

        return Ok(products);
    }
}