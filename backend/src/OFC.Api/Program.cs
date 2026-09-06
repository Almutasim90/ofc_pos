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
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
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

// Best-effort migration at startup; the API still boots if the DB is
// unavailable so the health endpoint keeps working.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Could not apply database migrations: {Message}", ex.Message);
    }
}

app.Run();

public partial class Program;
