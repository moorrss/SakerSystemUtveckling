using System.Text;
using JensenOnline.Api.Data;
using JensenOnline.Api.Models;
using JensenOnline.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<TokenService>();

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

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();   // vem är du?
app.UseAuthorization();    // vad får du göra?
app.MapControllers();

Directory.CreateDirectory("data");
await DbSeeder.SeedAsync(app.Services, app.Environment);

app.Run();