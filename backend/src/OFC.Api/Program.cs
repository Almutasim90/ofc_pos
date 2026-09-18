using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OFC.Api;
using OFC.Infrastructure;
using OFC.Api.Features;
using OFC.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<MigrationReadiness>();
builder.Services.AddHealthChecks()
    .AddCheck<MigrationReadinessCheck>("db-migrations", tags: ["ready"]);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddSingleton<IKitchenBroadcaster, KitchenBroadcaster>();
builder.Services.AddSingleton<IOrdersBroadcaster, OrdersBroadcaster>();
builder.Services.AddHostedService<KitchenFallbackWatcher>();
// The QR customer endpoints (/api/v1/qr/{code}...) are the only anonymous, unauthenticated routes in
// the API — open to menu-scraping and order-submission flooding with nothing else standing in the way.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // QR context codes are intentionally allowed to be short, staff-chosen table/pickup codes (e.g.
    // "T-01") rather than forced to be long random tokens, so this limit is the only real brake on
    // someone enumerating codes for this branch (security review finding M6) — it slows a single-IP
    // guesser but a determined attacker spreading requests across many IPs can still get around it.
    // Closing that fully would mean either dropping short custom codes or adding per-code lockout state.
    options.AddPolicy("qr-anonymous", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Login is the other anonymous route; without this it has no protection against
    // brute-force/credential-stuffing (security review finding M1).
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
// Defense-in-depth headers (security review finding L4). nginx.conf sets the same headers for the
// split web+api compose deployment; this covers the combined image where OFC.Api serves the SPA
// itself (see the fallback file mapping below) and every JSON API response either way.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: blob:; connect-src 'self' wss: https:; frame-ancestors 'none'");
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
// /health is a liveness probe and must stay OK while migrations are still retrying in the background
// (see the comment below) — without this predicate it silently included the "ready"-tagged
// db-migrations check by default (MapHealthChecks with no predicate runs every registered check),
// contradicting that stated intent and flapping a load balancer's liveness probe during every deploy.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => !check.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
app.MapSprintOneEndpoints();
app.MapSprintTwoEndpoints();
app.MapSprintThreeEndpoints();
app.MapSprintFourEndpoints();
app.MapSprintFiveEndpoints();
app.MapSprintSixEndpoints();
app.MapSprintSevenEndpoints();
app.MapSprintEightEndpoints();
app.MapSprintNineEndpoints();
app.MapSprintTenEndpoints();
app.MapSprintElevenEndpoints();
app.MapSprintTwelveEndpoints();
app.MapSprintThirteenEndpoints();
app.MapSprintFourteenEndpoints();
app.MapSprintFifteenEndpoints();
app.MapSprintSixteenEndpoints();
app.MapSprintSeventeenEndpoints();
app.MapSprintEighteenEndpoints();
app.MapHub<KitchenHub>("/hubs/kitchen");
app.MapHub<OrdersHub>("/hubs/orders");

var indexFile = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "index.html");
if (File.Exists(indexFile))
{
    app.MapFallbackToFile("index.html");
}

// Migrations run in the background so the API still boots (and /health stays
// live) even if the DB isn't reachable yet. /health/ready only turns healthy
// once migrations succeed, so dependents that wait on it (the seed job, the
// web container) don't race an unmigrated schema.
var readiness = app.Services.GetRequiredService<MigrationReadiness>();
_ = Task.Run(async () =>
{
    var delay = TimeSpan.FromSeconds(3);
    var maxDelay = TimeSpan.FromSeconds(30);
    while (true)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
            await db.Database.MigrateAsync();
            readiness.MarkReady();
            app.Logger.LogInformation("Database migrations applied.");
            return;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("Could not apply database migrations, retrying in {Delay}s: {Message}", delay.TotalSeconds, ex.Message);
            await Task.Delay(delay);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
        }
    }
});

app.Run();

public partial class Program;
