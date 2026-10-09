namespace JensenOnline.Api.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, bool success, string? details = null,
        string? userId = null, string? email = null);
}