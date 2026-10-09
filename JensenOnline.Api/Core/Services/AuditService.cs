using JensenOnline.Api.Data;
using JensenOnline.Api.Data.Entities;
using JensenOnline.Api.Core.Interfaces;

namespace JensenOnline.Api.Core.Services;

public class AuditService : IAuditService
{
    public static class Actions
    {
        public const string Register = "REGISTER";
        public const string LoginSuccess = "LOGIN_SUCCESS";
        public const string LoginFailed = "LOGIN_FAILED";
        public const string LoginLockedOut = "LOGIN_LOCKED_OUT";
        public const string Logout = "LOGOUT";
        public const string AccessDenied = "ACCESS_DENIED";
        public const string OrderCreated = "ORDER_CREATED";
        public const string OrderAccessDenied = "ORDER_ACCESS_DENIED";
        public const string ProductCreated = "PRODUCT_CREATED";
        public const string ProductUpdated = "PRODUCT_UPDATED";
        public const string ProductDeleted = "PRODUCT_DELETED";
        public const string UserLocked = "USER_LOCKED";
        public const string UserUnlocked = "USER_UNLOCKED";
    }

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task LogAsync(string action, bool success, string? details = null,
        string? userId = null, string? email = null)
    {
        var context = _http.HttpContext;
        var user = context?.User;

        var entry = new AuditLog
        {
            Action = action,
            Success = success,
            Details = details is { Length: > 500 } ? details[..500] : details,
            UserId = userId ?? user?.GetUserId(),
            Email = email ?? user?.GetEmail(),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString()
        };

        _logger.LogInformation("AUDIT {Action} success={Success} user={Email} ip={Ip} {Details}",
            entry.Action, entry.Success, entry.Email, entry.IpAddress, entry.Details);

        try
        {
            _db.AuditLogs.Add(entry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kunde inte skriva audit-logg för {Action}", action);
        }
    }
}