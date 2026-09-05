namespace OFC.Modules.Catalog;

public enum TaxCalculationMode { Exclusive = 0, Inclusive = 1 }
public enum PromotionDiscountType { Percentage = 0, FixedAmount = 1 }

public sealed class SalesChannel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PriceRule
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? SalesChannelId { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TaxRule
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TaxCategoryId { get; set; }
    public Guid? BranchId { get; set; }
    public decimal Rate { get; set; }
    public TaxCalculationMode CalculationMode { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Promotion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? SalesChannelId { get; set; }
    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CatalogVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public int Number { get; set; }
    public required string Note { get; set; }
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid PublishedByUserId { get; set; }
}

public sealed record PriceSnapshotDto(Guid ProductId, Guid BranchId, Guid SalesChannelId, DateTimeOffset ResolvedAt, decimal ListPrice, decimal DiscountAmount, decimal UnitNetAmount, decimal UnitTaxAmount, decimal UnitGrossAmount, decimal TaxRate, TaxCalculationMode TaxCalculationMode, string PriceSource, Guid? PriceRuleId, Guid? PromotionId, Guid? TaxRuleId, Guid? CatalogVersionId, int? CatalogVersionNumber, decimal? ManualOverridePrice, string? ManualOverrideReason);

public static class PricingRules
{
    public const int CodeMax = 50;
    public const int ReasonMax = 500;
    public static bool IsEffective(DateTimeOffset from, DateTimeOffset? to, DateTimeOffset at) => from <= at && (to is null || at < to);
    public static decimal RoundMoney(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero);

    public static PriceSnapshotDto Resolve(Product product, Guid branchId, Guid salesChannelId, DateTimeOffset at, IEnumerable<PriceRule> prices, IEnumerable<Promotion> promotions, IEnumerable<TaxRule> taxes, CatalogVersion? catalogVersion, decimal? manualOverridePrice = null, string? manualOverrideReason = null, decimal selectionAdjustment = 0m)
    {
        var candidates = prices.Where(x => x.ProductId == product.Id && x.IsActive && IsEffective(x.EffectiveFrom, x.EffectiveTo, at) && (x.BranchId is null || x.BranchId == branchId) && (x.SalesChannelId is null || x.SalesChannelId == salesChannelId))
            .OrderByDescending(x => x.BranchId.HasValue && x.SalesChannelId.HasValue).ThenByDescending(x => x.BranchId.HasValue).ThenByDescending(x => x.SalesChannelId.HasValue).ThenByDescending(x => x.EffectiveFrom).ToList();
        var priceRule = candidates.FirstOrDefault();
        var listPrice = (manualOverridePrice ?? priceRule?.Price ?? product.BasePrice ?? 0m) + selectionAdjustment;
        var promotion = manualOverridePrice is null ? promotions.Where(x => x.IsActive && IsEffective(x.EffectiveFrom, x.EffectiveTo, at) && (x.ProductId is null || x.ProductId == product.Id) && (x.BranchId is null || x.BranchId == branchId) && (x.SalesChannelId is null || x.SalesChannelId == salesChannelId)).OrderByDescending(x => x.Priority).ThenByDescending(x => x.EffectiveFrom).FirstOrDefault() : null;
        var discount = promotion is null ? 0m : promotion.DiscountType == PromotionDiscountType.Percentage ? listPrice * promotion.DiscountValue / 100m : promotion.DiscountValue;
        discount = RoundMoney(decimal.Min(listPrice, discount));
        var taxable = RoundMoney(listPrice - discount);
        var tax = product.TaxCategoryId is null ? null : taxes.Where(x => x.TaxCategoryId == product.TaxCategoryId && x.IsActive && IsEffective(x.EffectiveFrom, x.EffectiveTo, at) && (x.BranchId is null || x.BranchId == branchId)).OrderByDescending(x => x.BranchId.HasValue).ThenByDescending(x => x.EffectiveFrom).FirstOrDefault();
        var rate = tax?.Rate ?? 0m;
        var net = tax?.CalculationMode == TaxCalculationMode.Inclusive ? RoundMoney(taxable / (1m + rate / 100m)) : taxable;
        var taxAmount = RoundMoney(taxable - net);
        var gross = tax?.CalculationMode == TaxCalculationMode.Inclusive ? taxable : RoundMoney(net + RoundMoney(net * rate / 100m));
        if (tax?.CalculationMode != TaxCalculationMode.Inclusive) taxAmount = RoundMoney(gross - net);
        return new PriceSnapshotDto(product.Id, branchId, salesChannelId, at, RoundMoney(listPrice), discount, net, taxAmount, gross, rate, tax?.CalculationMode ?? TaxCalculationMode.Exclusive, manualOverridePrice is not null ? "ManualOverride" : priceRule is null ? "Default" : priceRule.BranchId.HasValue && priceRule.SalesChannelId.HasValue ? "BranchChannel" : priceRule.BranchId.HasValue ? "Branch" : priceRule.SalesChannelId.HasValue ? "Channel" : "PriceRule", priceRule?.Id, promotion?.Id, tax?.Id, catalogVersion?.Id, catalogVersion?.Number, manualOverridePrice, manualOverrideReason);
    }
}
