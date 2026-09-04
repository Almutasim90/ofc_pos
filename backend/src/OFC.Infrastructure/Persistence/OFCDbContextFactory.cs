using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OFC.Infrastructure.Persistence;

public sealed class OFCDbContextFactory : IDesignTimeDbContextFactory<OFCDbContext>
{
    public OFCDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The ConnectionStrings__DefaultConnection environment variable must be configured for EF tooling.");
        }

        var options = new DbContextOptionsBuilder<OFCDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OFCDbContext(options);
    }
}
