using JensenOnline.Api.Data;
using JensenOnline.Api.Dtos;
using JensenOnline.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JensenOnline.Api.Core.Interfaces;
using JensenOnline.Api.Core.Services;

namespace JensenOnline.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ProductsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts([FromQuery] ProductQuery query)
    {
        var products = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
       
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

        
     
        await _audit.LogAsync(AuditService.Actions.ProductCreated, true,
            $"Id {product.Id}: {product.Name}, pris {product.Price}");

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

        
        var oldPrice = product.Price; 

        product.Name = dto.Name.Trim();
        product.Description = dto.Description.Trim();
        product.Price = dto.Price;
        product.Stock = dto.Stock;
        await _db.SaveChangesAsync();

        
      
        await _audit.LogAsync(AuditService.Actions.ProductUpdated, true,
            $"Id {product.Id}: {product.Name}, pris {oldPrice} -> {product.Price}");


        return Ok(new ProductDto(product.Id, product.Name, product.Description, product.Price, product.Stock));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = DbSeeder.AdminRole)]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

       
        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
            return Problem(title: "Produkten finns i ordrar och kan inte tas bort.", statusCode: StatusCodes.Status409Conflict);

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditService.Actions.ProductDeleted, true,
            $"Id {id}: {product.Name}");

        return NoContent();
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}