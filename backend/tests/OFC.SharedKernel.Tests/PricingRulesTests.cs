using OFC.Modules.Catalog;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class PricingRulesTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
    private static Product Product(Guid? taxCategoryId = null) => new() { Id = Guid.CreateVersion7(), Sku = "TEST", NameAr = "اختبار", NameEn = "Test", CategoryId = Guid.CreateVersion7(), BasePrice = 10m, TaxCategoryId = taxCategoryId };

    [Fact]
    public void Resolve_prefers_branch_and_channel_price_over_all_other_prices()
    {
        var product = Product(); var branch = Guid.CreateVersion7(); var channel = Guid.CreateVersion7();
        var result = PricingRules.Resolve(product, branch, channel, At, [new PriceRule { ProductId = product.Id, Price = 11m, EffectiveFrom = At.AddDays(-1) }, new PriceRule { ProductId = product.Id, BranchId = branch, Price = 12m, EffectiveFrom = At.AddDays(-1) }, new PriceRule { ProductId = product.Id, SalesChannelId = channel, Price = 13m, EffectiveFrom = At.AddDays(-1) }, new PriceRule { ProductId = product.Id, BranchId = branch, SalesChannelId = channel, Price = 14m, EffectiveFrom = At.AddDays(-1) }], [], [], null);

        Assert.Equal(14m, result.ListPrice);
        Assert.Equal("BranchChannel", result.PriceSource);
    }

    [Fact]
    public void Resolve_applies_effective_inclusive_tax_and_promotion()
    {
        var taxCategory = Guid.CreateVersion7(); var product = Product(taxCategory); var branch = Guid.CreateVersion7(); var channel = Guid.CreateVersion7();
        var result = PricingRules.Resolve(product, branch, channel, At, [], [new Promotion { Code = "TEN", NameAr = "خصم", NameEn = "Discount", DiscountType = PromotionDiscountType.Percentage, DiscountValue = 10m, EffectiveFrom = At.AddDays(-1) }], [new TaxRule { TaxCategoryId = taxCategory, Rate = 5m, CalculationMode = TaxCalculationMode.Inclusive, EffectiveFrom = At.AddDays(-1) }], null);

        Assert.Equal(9m, result.UnitGrossAmount);
        Assert.Equal(8.571m, result.UnitNetAmount);
        Assert.Equal(.429m, result.UnitTaxAmount);
    }

    [Fact]
    public void Resolve_applies_effective_exclusive_tax()
    {
        // The exclusive branch is a separate code path from inclusive (Resolve recomputes gross/tax
        // differently per CalculationMode) and previously had no dedicated coverage of its own.
        var taxCategory = Guid.CreateVersion7(); var product = Product(taxCategory); var branch = Guid.CreateVersion7(); var channel = Guid.CreateVersion7();
        var result = PricingRules.Resolve(product, branch, channel, At, [], [], [new TaxRule { TaxCategoryId = taxCategory, Rate = 5m, CalculationMode = TaxCalculationMode.Exclusive, EffectiveFrom = At.AddDays(-1) }], null);

        Assert.Equal(10m, result.UnitNetAmount);
        Assert.Equal(.5m, result.UnitTaxAmount);
        Assert.Equal(10.5m, result.UnitGrossAmount);
    }

    [Fact]
    public void Resolve_uses_manual_override_without_promotion()
    {
        var product = Product(); var result = PricingRules.Resolve(product, Guid.CreateVersion7(), Guid.CreateVersion7(), At, [], [new Promotion { Code = "HALF", NameAr = "خصم", NameEn = "Discount", DiscountType = PromotionDiscountType.Percentage, DiscountValue = 50m, EffectiveFrom = At.AddDays(-1) }], [], null, 7m, "Manager approval");

        Assert.Equal(7m, result.UnitGrossAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal("ManualOverride", result.PriceSource);
        Assert.Equal("Manager approval", result.ManualOverrideReason);
    }

    [Fact]
    public void Resolve_adds_selection_adjustment_to_channel_rule_price()
    {
        var product = Product(); var branch = Guid.CreateVersion7(); var channel = Guid.CreateVersion7();
        var result = PricingRules.Resolve(product, branch, channel, At, [new PriceRule { ProductId = product.Id, SalesChannelId = channel, Price = 12m, EffectiveFrom = At.AddDays(-1) }], [], [], null, selectionAdjustment: .5m);

        Assert.Equal(12.5m, result.ListPrice);
        Assert.Equal(12.5m, result.UnitGrossAmount);
    }
}
