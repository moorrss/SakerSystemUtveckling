using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JensenOnline.Api.ErrorHandling;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
      
        var errorId = $"ERR-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        _logger.LogError(exception, "Ohanterat fel {ErrorId} vid {Method} {Path}",
            errorId, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ett fel uppstod. Försök igen senare.",
            Extensions = { ["errorId"] = errorId }
        }, cancellationToken);

        return true;
    }
}