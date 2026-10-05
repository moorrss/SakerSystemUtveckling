using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JensenOnline.Api.ErrorHandling;

//Global felhantering. Alla fel som inte fångas någon annanstans i applikationen hamnar här (T7)
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
        // 1. Skapa en felkod som användaren kan uppge vid kontakt med supporten
        var errorId = $"ERR-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        // 2. Logga HELA felet med stack trace. Detaljerna hamnar bara i loggen
        _logger.LogError(exception, "Ohanterat fel {ErrorId} vid {Method} {Path}",
            errorId, httpContext.Request.Method, httpContext.Request.Path);

        // 3. Skicka ett generiskt svar. Exception.Message och stack trace läcker aldrig ut till klienten
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ett fel uppstod. Försök igen senare.",
            Extensions = { ["errorId"] = errorId }
        }, cancellationToken);

        return true; // true = felet är hanterat och pipelinen stannar här
    }
}