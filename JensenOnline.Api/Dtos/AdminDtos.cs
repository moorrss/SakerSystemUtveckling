namespace JensenOnline.Api.Dtos;

public record AdminOrderDto(int Id, string CustomerEmail, DateTime CreatedAt, decimal TotalAmount, int ItemCount);

// Lösenordshashar och säkerhetsstämplar skickas aldrig ut
public record AdminUserDto(string Id, string Email, IList<string> Roles, bool IsLockedOut);

public class SetLockDto
{
    public bool Locked { get; set; }
}

// Det admin ser av audit-loggen. Bara läsning, det finns ingen endpoint för att ändra eller radera (T5)
public record AuditLogDto(DateTime Timestamp, string? Email, string Action, string? Details, string? IpAddress, bool Success);