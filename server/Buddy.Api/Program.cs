using System.Threading.RateLimiting;
using Buddy.Api.Application.Authentication;
using Buddy.Api.Application.Capabilities;
using Buddy.Api.Application.FamilyManagement;
using Buddy.Api.Application.Memory;
using Buddy.Api.Application.Safety;
using Buddy.Api.Application.SessionManagement;
using Buddy.Api.Application.Summary;
using Buddy.Api.Endpoints;
using Buddy.Api.Infrastructure.Persistence;
using Buddy.Api.Models;
using Buddy.Api.Services;
using Buddy.Api.Workers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database Configuration: Azure SQL for production, SQLite for local dev
// ---------------------------------------------------------------------------
var sqlServerConn = builder.Configuration.GetConnectionString("AzureSql");
if (!string.IsNullOrWhiteSpace(sqlServerConn))
{
    builder.Services.AddDbContext<BuddyDbContext>(o => o.UseSqlServer(sqlServerConn));
}
else
{
    var sqliteConn = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=buddy.db";
    builder.Services.AddDbContext<BuddyDbContext>(o => o.UseSqlite(sqliteConn));
}

// ---------------------------------------------------------------------------
// Configuration – fail fast at startup instead of on the first user request
// ---------------------------------------------------------------------------
builder.Services.AddOptions<AzureRealtimeOptions>()
    .BindConfiguration(AzureRealtimeOptions.SectionName)
    .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out _), "AzureRealtime:Endpoint must be an absolute URL.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "AzureRealtime:ApiKey is required (use user-secrets locally).")
    .Validate(o => !string.IsNullOrWhiteSpace(o.DeploymentName), "AzureRealtime:DeploymentName is required.")
    .ValidateOnStart();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddHttpClient();

// Domain & Application Services
builder.Services.AddSingleton<PersonaService>();
builder.Services.AddScoped<RealtimeSessionService>();
builder.Services.AddScoped<IGuardianIdentityProvider, DevelopmentGuardianIdentityProvider>();
builder.Services.AddScoped<GuardianAuthService>();
builder.Services.AddScoped<FamilyService>();
builder.Services.AddScoped<ConsentService>();
builder.Services.AddScoped<SessionLeaseService>();
builder.Services.AddScoped<SessionManager>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddScoped<DiscoveryDigestService>();
builder.Services.AddScoped<SafetyAuditService>();

// Capability Tools
builder.Services.AddSingleton<IPublicSearchProvider, DevelopmentSearchProvider>();
builder.Services.AddScoped<IBuddyTool, TimeTool>();
builder.Services.AddScoped<IBuddyTool, CalculatorTool>();
builder.Services.AddScoped<IBuddyTool, WeatherTool>();
builder.Services.AddScoped<IBuddyTool, PublicSearchTool>();
builder.Services.AddScoped<ToolRegistry>();

// Outbox Worker
builder.Services.AddHostedService<OutboxBackgroundService>();

// ---------------------------------------------------------------------------
// CORS – origins come from configuration ("Cors:AllowedOrigins")
// ---------------------------------------------------------------------------
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "https://localhost:5173", "http://127.0.0.1:5173", "https://127.0.0.1:5173"];

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(origins)
    .WithHeaders("Content-Type", "X-Family-Id", "X-Guardian-Id", "X-Child-Profile-Id", "X-Device-Id")
    .WithMethods("POST", "GET", "PUT", "DELETE")));

// ---------------------------------------------------------------------------
// Rate limiting
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("session", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure DB is created and seeded in Development
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BuddyDbContext>();
    db.Database.EnsureCreated();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, phase = "Phase 1" }))
   .WithName("HealthCheck")
   .WithTags("Health");

// Map all Phase 1 Minimal API Endpoints
app.MapBuddyEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

// Export Program for WebApplicationFactory tests
public partial class Program { }
