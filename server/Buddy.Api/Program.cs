using System.Threading.RateLimiting;
using Buddy.Api.Models;
using Buddy.Api.Services;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration – fail fast at startup instead of on the first user request
// ---------------------------------------------------------------------------
builder.Services.AddOptions<AzureRealtimeOptions>()
    .BindConfiguration(AzureRealtimeOptions.SectionName)
    .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out _), "AzureRealtime:Endpoint must be an absolute URL.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "AzureRealtime:ApiKey is required (use user-secrets locally).")
    .Validate(o => !string.IsNullOrWhiteSpace(o.DeploymentName), "AzureRealtime:DeploymentName is required.")
    .ValidateOnStart();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PersonaService>();
builder.Services.AddScoped<RealtimeSessionService>();

// ---------------------------------------------------------------------------
// CORS – origins come from configuration ("Cors:AllowedOrigins"), so production
// hosts are a config change, not a code change.
// ---------------------------------------------------------------------------
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "https://localhost:5173", "http://127.0.0.1:5173", "https://127.0.0.1:5173"];

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(origins)
    .WithHeaders("Content-Type")
    .WithMethods("POST", "GET")));

// ---------------------------------------------------------------------------
// Rate limiting – every successful call mints a billable Azure session, and
// this endpoint has no login. Without a limit, anyone who finds the URL can
// run up the bill.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("session", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler(); // unexpected errors -> generic ProblemDetails, no stack trace / upstream text

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

app.MapPost("/api/realtime/session", async (
        CreateSessionRequest request,
        RealtimeSessionService sessionService,
        IHostEnvironment env,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        if (!PersonaService.IsValidMode(request.Mode))
        {
            return Results.BadRequest(new { error = "Mode must be 'adult' or 'child'." });
        }

        // The Gate-0 diagnostic flag is for local debugging only.
        if (!env.IsDevelopment())
        {
            request.MinimalConfig = false;
        }

        try
        {
            return Results.Ok(await sessionService.CreateSessionAsync(request, ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException)
        {
            // Details are logged by the service; do not echo upstream text to the caller.
            logger.LogError(ex, "Creating realtime session failed");
            return Results.Problem(
                title: "Realtime session could not be created",
                statusCode: StatusCodes.Status502BadGateway);
        }
    })
   .RequireRateLimiting("session")
   .WithName("CreateRealtimeSession")
   .WithTags("Realtime")
   .Produces<CreateSessionResponse>()
   .ProducesProblem(StatusCodes.Status400BadRequest)
   .ProducesProblem(StatusCodes.Status429TooManyRequests)
   .ProducesProblem(StatusCodes.Status502BadGateway);

app.MapFallbackToFile("index.html");

app.Run();
