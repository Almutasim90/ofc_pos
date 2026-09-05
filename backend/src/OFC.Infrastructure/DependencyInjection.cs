using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;

namespace OFC.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("OFC");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                "[OFC] Warning: no PostgreSQL connection string configured (ConnectionStrings__DefaultConnection). " +
                "The API will start, but database-backed endpoints will fail until it is set.");
        }
        else
        {
            services.AddDbContext<OFCDbContext>(options => options.UseNpgsql(connectionString));
        }

        services.AddScoped<IdentityService>();
        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);
        services.AddAuthorizationBuilder().AddPolicy("permission", policy => policy.RequireAuthenticatedUser());

        return services;
    }
}
