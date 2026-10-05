using System.Threading.RateLimiting;
using JensenOnline.Api.Middleware;
using JensenOnline.Api.ErrorHandling;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using JensenOnline.Api.Data;
using JensenOnline.Api.Data.Entities;
using JensenOnline.Api.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JensenOnline.Api.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ---------- Kestrel: dölj serverversion och begränsa anropens storlek (T7, T8) ----------
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;                   // ingen "Server: Kestrel" i svaren
    options.Limits.MaxRequestBodySize = 1024 * 1024;   // max 1 MB per anrop
});

// ---------- Databas (T2) ----------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// ---------- Identity: hashning, password policy och lockout (T1) ----------
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        // Password policy: längd är viktigare än komplexitet
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // Account lockout: låst i 15 minuter efter 5 misslyckade försök
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

// ---------- JWT-nyckeln kommer från .env, aldrig från koden (T7) ----------
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    // Fail securely: appen startar inte utan en riktig nyckel
    throw new InvalidOperationException("Jwt:Key saknas eller är för kort (minst 32 byte).");
}

var jwtSettings = new JwtSettings
{
    Issuer = builder.Configuration["Jwt:Issuer"]!,
    Audience = builder.Configuration["Jwt:Audience"]!,
    LifetimeMinutes = builder.Configuration.GetValue("Jwt:LifetimeMinutes", 30),
    SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
};
builder.Services.AddSingleton(jwtSettings);

//Här sätts DI upp för våra tjänster. Controllers känner bara till interfacet, inte klassen
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddHttpContextAccessor();   // ger AuditService tillgång till IP och inloggad användare
builder.Services.AddScoped<IAuditService, AuditService>();

// ---------- Autentisering: JWT som läses från HttpOnly-cookien ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // behåll claim-namnen sub, email och role
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = jwtSettings.SigningKey,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, // "alg": "none" avvisas
            NameClaimType = JwtRegisteredClaimNames.Email,
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            // Hämta token från cookien i stället för Authorization-headern
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies[TokenService.CookieName];
                return Task.CompletedTask;
            },
            
            // Körs när en inloggad användare saknar rätt roll (403), t.ex. en kund som anropar /api/admin.
            // Försöket loggas så att det går att spåra i efterhand (T5)
            OnForbidden = async context =>
            {
                var audit = context.HttpContext.RequestServices.GetRequiredService<IAuditService>();
                await audit.LogAsync(AuditService.Actions.AccessDenied, false,
                    $"{context.Request.Method} {context.Request.Path}");
            }
        };
    });

// ---------- Auktorisering: secure by default ----------
// Alla endpoints kräver inloggning om de inte är märkta med [AllowAnonymous]
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ---------- Rate limiting: skydd mot brute force och överbelastning (T1, T8) ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Alla anrop: max 200 per minut och IP-adress
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Inloggning och registrering: max 20 per minut och IP-adress
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "För många anrop. Försök igen om en stund."
        }, cancellationToken);
    };
});

// ---------- Controllers och säker felhantering (T7) ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Trasig JSON ger ett generiskt meddelande, inte tolkarens interna felbeskrivning
        options.AllowInputFormatterExceptionMessages = false;
    });

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ---------- Central felhantering (T7) ----------
// Användaren får ett generiskt svar med en felkod. Detaljerna hamnar BARA i loggen.
app.UseExceptionHandler();

// Tomma felsvar (401, 403, 404) får ett enhetligt ProblemDetails-svar
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();   // vem är du?
app.UseAuthorization();    // vad får du göra?
app.MapControllers();

Directory.CreateDirectory("data");
await DbSeeder.SeedAsync(app.Services, app.Environment);

app.Run();