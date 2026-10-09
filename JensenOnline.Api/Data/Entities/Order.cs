namespace JensenOnline.Api.Data.Entities;
public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ShippingAddress { get; set; } = "";
    public decimal TotalAmount { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}