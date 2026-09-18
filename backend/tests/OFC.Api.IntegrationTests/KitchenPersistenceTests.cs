using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OFC.Infrastructure.Persistence;
using OFC.Modules.Kitchen;
using OFC.Modules.Ordering;
using OFC.Modules.Printing;
using Xunit;

namespace OFC.Api.IntegrationTests;

public sealed class KitchenPersistenceTests
{
    private static OFCDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<OFCDbContext>()
            .UseNpgsql("Host=localhost;Database=ofc_model_tests;Username=unused;Password=unused")
            .Options;

        return new OFCDbContext(options);
    }

    [Fact]
    public void One_dispatch_can_create_one_ticket_per_preparation_station()
    {
        using var db = CreateModelContext();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(KitchenTicket))!;
        var index = entity.GetIndexes().Single(x => x.Properties.Select(p => p.Name)
            .SequenceEqual([nameof(KitchenTicket.BranchId), nameof(KitchenTicket.DispatchId), nameof(KitchenTicket.StationId)]));

        Assert.True(index.IsUnique);
        Assert.False(index.GetAreNullsDistinct());
    }

    [Fact]
    public void Automatic_print_jobs_can_be_owned_by_the_system()
    {
        using var db = CreateModelContext();
        var property = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PrintJob))!.FindProperty(nameof(PrintJob.CreatedByUserId))!;

        Assert.True(property.IsNullable);
    }

    [Fact]
    public void Customer_order_number_is_unique_and_generated_by_the_database()
    {
        using var db = CreateModelContext();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Order))!;
        var number = entity.FindProperty(nameof(Order.Number))!;
        var uniqueIndex = entity.GetIndexes().Single(x => x.Properties.Count == 1 && x.Properties[0] == number);

        Assert.Equal(ValueGenerated.OnAdd, number.ValueGenerated);
        Assert.True(uniqueIndex.IsUnique);
    }
}
