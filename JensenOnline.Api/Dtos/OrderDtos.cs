using System.ComponentModel.DataAnnotations;

namespace JensenOnline.Api.Dtos;

public class CreateOrderDto
{
    [Required, MinLength(1), MaxLength(20)]
    public List<OrderItemInputDto> Items { get; set; } = new();

    [Required, StringLength(200, MinimumLength = 5)]
    public string ShippingAddress { get; set; } = "";
}

public class OrderItemInputDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}

public record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public record OrderDto(int Id, DateTime CreatedAt, string ShippingAddress, decimal TotalAmount, IReadOnlyList<OrderItemDto> Items);