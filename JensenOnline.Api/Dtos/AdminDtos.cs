namespace JensenOnline.Api.Dtos;

public record AdminOrderDto(int Id, string CustomerEmail, DateTime CreatedAt, decimal TotalAmount, int ItemCount);

// Lösenordshashar och säkerhetsstämplar skickas aldrig ut
public record AdminUserDto(string Id, string Email, IList<string> Roles, bool IsLockedOut);

public class SetLockDto
{
    public bool Locked { get; set; }
}