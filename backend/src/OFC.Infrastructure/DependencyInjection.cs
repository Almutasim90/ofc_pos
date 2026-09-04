using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using OFC.Infrastructure.Persistence;

namespace OFC.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("OFC");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The ConnectionStrings__DefaultConnection environment variable must be configured.");
        }

        services.AddDbContext<OFCDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
