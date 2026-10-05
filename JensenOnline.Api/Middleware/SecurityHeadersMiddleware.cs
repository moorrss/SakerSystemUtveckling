namespace JensenOnline.Api.Middleware;

// Security headers ger webbläsaren instruktioner om hur sidan får användas (T4).
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Content Security Policy: bara skript, stilar och bilder från den egna domänen.
            // Inline-skript och onclick-attribut blockeras, så även om en XSS-payload skulle
            // hamna på sidan körs den inte. frame-ancestors 'none' skyddar mot clickjacking.
            headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
                "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

            // Webbläsaren får inte gissa filtyp (t.ex. köra en textfil som skript)
            headers["X-Content-Type-Options"] = "nosniff";

            // Sidan får inte bäddas in på andra sajter (clickjacking)
            headers["X-Frame-Options"] = "DENY";

            // Skicka inte vår URL vidare till andra sajter
            headers["Referrer-Policy"] = "no-referrer";

            // Stäng av webbläsarfunktioner som webbshoppen inte behöver
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // Svar från API:t med personuppgifter ska inte sparas i cachen
            if (context.Request.Path.StartsWithSegments("/api"))
                headers["Cache-Control"] = "no-store";

            return Task.CompletedTask;
        });

        return _next(context);
    }
}