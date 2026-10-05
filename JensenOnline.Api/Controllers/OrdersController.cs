using JensenOnline.Api.Data;
using JensenOnline.Api.Dtos;
using JensenOnline.Api.Models;
using JensenOnline.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JensenOnline.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize] // alla order-endpoints kräver inloggning
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db) => _db = db;

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto dto)
    {
        var userId = User.GetUserId()!;

        // Slå ihop rader med samma produkt
        var requested = dto.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var productIds = requested.Keys.ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Steg 1: kontrollera allt innan något ändras
        foreach (var (productId, quantity) in requested)
        {
            if (!products.TryGetValue(productId, out var product))
                return Problem(title: $"Produkten med id {productId} finns inte.", statusCode: StatusCodes.Status400BadRequest);
            if (quantity > 100)
                return Problem(title: "Max 100 st per produkt.", statusCode: StatusCodes.Status400BadRequest);
            if (product.Stock < quantity)
                return Problem(title: $"Det finns inte tillräckligt många '{product.Name}' i lager.", statusCode: StatusCodes.Status409Conflict);
        }

        var order = new Order
        {
            UserId = userId, // ägaren kommer från token, aldrig från klienten (T6)
            ShippingAddress = dto.ShippingAddress.Trim()
        };

        // Steg 2: skapa orderrader med priset FRÅN DATABASEN (T3)
        foreach (var (productId, quantity) in requested)
        {
            var product = products[productId];
            product.Stock -= quantity;
            order.Items.Add(new OrderItem { ProductId = productId, Quantity = quantity, UnitPrice = product.Price });
        }

        // Totalsumman räknas ut av servern
        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ToDto(order));
    }

    // Bara den inloggade användarens egna ordrar (T6)
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetMine()
    {
        var userId = User.GetUserId()!;
        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .OrderByDescending(o => o.Id)
            .ToListAsync();

        return Ok(orders.Select(ToDto).ToList());
    }

    // Skydd mot IDOR (T6): ordern måste tillhöra användaren, eller så måste användaren vara admin.
    // Rollen räcker inte ensam, eftersom ALLA kunder har rollen Customer.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var userId = User.GetUserId()!;
        var isAdmin = User.IsInRole(DbSeeder.AdminRole);

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && (o.UserId == userId || isAdmin));

        // 404 i stället för 403, så att angriparen inte ens får veta att ordern finns
        if (order is null) return NotFound();

        return Ok(ToDto(order));
    }

    private static OrderDto ToDto(Order o) =>
        new(o.Id, o.CreatedAt, o.ShippingAddress, o.TotalAmount,
            o.Items.Select(i => new OrderItemDto(i.ProductId, i.Product?.Name ?? "", i.Quantity, i.UnitPrice)).ToList());
}