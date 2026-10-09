using JensenOnline.Api.Data;
using JensenOnline.Api.Dtos;
using JensenOnline.Api.Data.Entities;
using JensenOnline.Api.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JensenOnline.Api.Core.Interfaces;

namespace JensenOnline.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize] 
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

   
    public OrdersController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

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
            UserId = userId,
            ShippingAddress = dto.ShippingAddress.Trim()
        };

   
        foreach (var (productId, quantity) in requested)
        {
            var product = products[productId];
            product.Stock -= quantity;
            order.Items.Add(new OrderItem { ProductId = productId, Quantity = quantity, UnitPrice = product.Price });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

      
        await _audit.LogAsync(AuditService.Actions.OrderCreated, true,
            $"Order {order.Id}, totalt {order.TotalAmount} kr");

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ToDto(order));
    }

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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var userId = User.GetUserId()!;
        var isAdmin = User.IsInRole(DbSeeder.AdminRole);

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && (o.UserId == userId || isAdmin));

        if (order is null)
        {
        
            if (await _db.Orders.AnyAsync(o => o.Id == id))
                await _audit.LogAsync(AuditService.Actions.OrderAccessDenied, false, $"Försökte läsa order {id}");

            return NotFound();
        }

        return Ok(ToDto(order));
    }

    private static OrderDto ToDto(Order o) =>
        new(o.Id, o.CreatedAt, o.ShippingAddress, o.TotalAmount,
            o.Items.Select(i => new OrderItemDto(i.ProductId, i.Product?.Name ?? "", i.Quantity, i.UnitPrice)).ToList());
}