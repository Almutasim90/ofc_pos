using OFC.Modules.Catalog;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class SelectionGroupTests
{
    [Fact]
    public void Calculation_sums_selected_price_adjustments()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var group = new SelectionGroup
        {
            NameAr = "إضافات", NameEn = "Extras", MinSelections = 1, MaxSelections = 2,
            Options = [new SelectionOption { Id = first, ProductId = Guid.CreateVersion7(), PriceAdjustment = .200m }, new SelectionOption { Id = second, ProductId = Guid.CreateVersion7(), PriceAdjustment = .300m }]
        };

        var result = CatalogRules.Calculate(group, [new SelectionChoice(first, 1), new SelectionChoice(second, 1)]);

        Assert.True(result.IsValid);
        Assert.Equal(.500m, result.PriceAdjustment);
    }

    [Fact]
    public void Calculation_rejects_required_group_without_selection()
    {
        var group = new SelectionGroup { NameAr = "مشروب", NameEn = "Drink", IsRequired = true, MinSelections = 1, MaxSelections = 1 };

        var result = CatalogRules.Calculate(group, []);

        Assert.False(result.IsValid);
        Assert.Equal(0m, result.PriceAdjustment);
    }

    [Theory]
    [InlineData(true, 0, 1, false)]
    [InlineData(false, 0, 2, true)]
    [InlineData(true, 1, 1, true)]
    [InlineData(false, 2, 1, false)]
    public void Selection_range_enforces_required_minimum(bool required, int min, int max, bool expected)
    {
        Assert.Equal(expected, CatalogRules.ValidSelectionRange(required, min, max));
    }
}
