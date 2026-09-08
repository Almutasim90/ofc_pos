using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
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
