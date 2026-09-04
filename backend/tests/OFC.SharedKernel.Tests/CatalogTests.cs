using OFC.Modules.Catalog;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void Product_type_exposes_the_supported_kinds()
    {
        var values = Enum.GetValues<ProductType>();

        Assert.Contains(ProductType.Simple, values);
        Assert.Contains(ProductType.Combo, values);
        Assert.Contains(ProductType.Service, values);
    }

    [Fact]
    public void New_product_defaults_to_active_simple_product()
    {
        var product = new Product { Sku = "ZNG-1", NameAr = "برجر", NameEn = "Burger" };

        Assert.True(product.IsActive);
        Assert.Equal(ProductType.Simple, product.Type);
        Assert.Equal(7, product.Id.Version);
        Assert.Equal(DateTimeOffset.UtcNow.Date, product.CreatedAt.Date);
    }

    [Fact]
    public void Availability_defaults_to_available()
    {
        var availability = new ProductBranchAvailability { ProductId = Guid.CreateVersion7(), BranchId = Guid.CreateVersion7() };

        Assert.True(availability.IsAvailable);
    }

    public static TheoryData<decimal?, bool> NonNegativePriceCases => new() { { null, true }, { 0m, true }, { 1.5m, true }, { -0.01m, false } };

    [Theory]
    [MemberData(nameof(NonNegativePriceCases))]
    public void Non_negative_price_rule_rejects_negative_values(decimal? value, bool expected)
    {
        Assert.Equal(expected, CatalogRules.ValidNonNegativePrice(value));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(12, true)]
    [InlineData(10000, false)]
    public void Sort_order_is_bounded(int? value, bool expected)
    {
        Assert.Equal(expected, CatalogRules.ValidSortOrder(value));
    }
}
