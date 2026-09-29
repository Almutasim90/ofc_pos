using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OFC.Infrastructure.Persistence;
using Xunit;

namespace OFC.Api.IntegrationTests;

// Enums are stored by name in length-limited text columns. The in-memory test database never enforces
// those lengths, so an over-long name (QrApprovalMode.RequiresStaffApproval in varchar(20)) only failed
// in production, as a 500 on save. This checks every enum value against its column.
public class EnumColumnLengthTests
{
    [Fact]
    public void Every_enum_value_fits_its_column()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<OFCDbContext>().Model;
        var tooLong = (
            from entity in model.GetEntityTypes()
            from property in entity.GetProperties()
            let enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType
            where enumType.IsEnum && property.GetMaxLength() is not null
            from name in Enum.GetNames(enumType)
            where name.Length > property.GetMaxLength()
            select $"{entity.ClrType.Name}.{property.Name} = {name} ({name.Length} > {property.GetMaxLength()})"
        ).ToList();
        Assert.True(tooLong.Count == 0, string.Join("\n", tooLong));
    }
}
