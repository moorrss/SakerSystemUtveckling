namespace JensenOnline.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
                "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

            headers["X-Content-Type-Options"] = "nosniff";

            headers["X-Frame-Options"] = "DENY";

     
            headers["Referrer-Policy"] = "no-referrer";


            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            if (context.Request.Path.StartsWithSegments("/api"))
                headers["Cache-Control"] = "no-store";

            return Task.CompletedTask;
        });

        return _next(context);
    }
}