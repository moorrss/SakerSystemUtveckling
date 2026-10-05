using JensenOnline.Api.Data;
using JensenOnline.Api.Dtos;
using JensenOnline.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JensenOnline.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    // Visa och söka är öppet för alla
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts([FromQuery] ProductQuery query)
    {
        var products = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Söktermen skickas som en PARAMETER till databasen och kan aldrig köras som SQL (T2).
            // % och _ escapas så att de söks som vanliga tecken.
            var pattern = "%" + EscapeLike(query.Search.Trim()) + "%";
            products = products.Where(p =>
                EF.Functions.Like(p.Name, pattern, "\\") ||
                EF.Functions.Like(p.Description, pattern, "\\"));
        }

        var total = await products.CountAsync();
        var items = await products
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock))
            .ToListAsync();

        return Ok(new PagedResult<ProductDto>(items, query.Page, query.PageSize, total));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        return Ok(new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock));
    }

    // Skapa, ändra och ta bort kräver rollen Admin. Kontrollen sker HÄR i backend,
    // så det hjälper inte att anropa API:t direkt och kringgå frontend (T9).
    [HttpPost]
    [Authorize(Roles = DbSeeder.AdminRole)]
    [Consumes("application/json")]
    public async Task<ActionResult<ProductDto>> Create(ProductInputDto dto)
    {
        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            Price = dto.Price,
            Stock = dto.Stock
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var result = new ProductDto(product.Id, product.Name, product.Description, product.Price, product.Stock);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = DbSeeder.AdminRole)]
    [Consumes("application/json")]
    public async Task<ActionResult<ProductDto>> Update(int id, ProductInputDto dto)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

        product.Name = dto.Name.Trim();
        product.Description = dto.Description.Trim();
        product.Price = dto.Price;
        product.Stock = dto.Stock;
        await _db.SaveChangesAsync();

        return Ok(new ProductDto(product.Id, product.Name, product.Description, product.Price, product.Stock));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = DbSeeder.AdminRole)]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

        // Produkter som finns i ordrar får inte tas bort, då förstörs orderhistoriken
        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
            return Problem(title: "Produkten finns i ordrar och kan inte tas bort.", statusCode: StatusCodes.Status409Conflict);

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}