using Microsoft.EntityFrameworkCore;

namespace OFC.Infrastructure.Persistence;

public sealed class OFCDbContext(DbContextOptions<OFCDbContext> options) : DbContext(options)
{
    public const string Schema = "ofc";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
    }
}
