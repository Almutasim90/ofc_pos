using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OFC.Infrastructure.Persistence;
using System.Net.Http.Json;

namespace OFC.Api.IntegrationTests;

// The real backend runs against PostgreSQL (jsonb columns, check constraints, atomic
// ExecuteUpdateAsync SQL) which this factory's EF Core InMemory provider does not fully reproduce —
// so a handful of endpoints that lean on raw SQL (e.g. InventoryStock.ApplyDelta) aren't exercised
// at full fidelity here. What this DOES give, for the first time anywhere in this codebase, is the
// full HTTP pipeline: routing, the Session auth handler, permission-claim authorization, model
// binding/validation, and the endpoint + Rules-class business logic together — the exact gap every
// sprint audit finding about "no integration tests" pointed at.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public readonly string DatabaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // AddInfrastructure only calls AddDbContext<OFCDbContext> when a connection string is present,
        // so a dummy one is supplied here purely to make that registration happen — it is never
        // connected to, because the InMemory provider below replaces it before the host starts.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=unused;Username=unused;Password=unused",
        }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OFCDbContext>>();
            services.AddDbContext<OFCDbContext>(options => options.UseInMemoryDatabase(DatabaseName));
            // A 500 in these tests is a real failure to diagnose, not something to just retry — surface
            // the actual exception in the ProblemDetails body instead of the production-safe generic one.
            services.Configure<Microsoft.AspNetCore.Http.ProblemDetailsOptions>(options => options.CustomizeProblemDetails = ctx =>
            {
                var error = ctx.HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
                if (error is not null) ctx.ProblemDetails.Extensions["exception"] = error.ToString();
                if (error is DbUpdateException dbEx) ctx.ProblemDetails.Extensions["entries"] = string.Join(" | ", dbEx.Entries.Select(e => $"{e.Entity.GetType().Name}:{e.State}"));
            });
        });
    }

    public HttpClient AnonymousClient() => CreateClient();

    public async Task<(HttpClient Client, string Token)> AuthenticatedClientAsync(string username, string password, Guid? branchId = null, Guid? deviceId = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password, branchId, deviceId });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Token);
        return (client, body.Token);
    }

    private sealed record LoginResponse(string Token);
}
