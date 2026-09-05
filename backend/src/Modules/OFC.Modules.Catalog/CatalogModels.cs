namespace OFC.Modules.Catalog;

public enum ProductType
{
    Simple = 0,
    Combo = 1,
    Service = 2
}

public enum SelectionGroupKind
{
    Combo = 0,
    Modifier = 1
}

public sealed class Category
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public int SortOrder { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Sku { get; set; }
    public string? Barcode { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public ProductType Type { get; set; } = ProductType.Simple;
    public Guid? TaxCategoryId { get; set; }
    public TaxCategory? TaxCategory { get; set; }
    public Guid? PreparationStationId { get; set; }
    public PreparationStation? PreparationStation { get; set; }
    public decimal? BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductBranchAvailability> BranchAvailability { get; set; } = [];
    public ICollection<ProductSelectionGroup> SelectionGroups { get; set; } = [];
}

public sealed class SelectionGroup
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public SelectionGroupKind Kind { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public bool IsRequired { get; set; }
    public int MinSelections { get; set; }
    public int MaxSelections { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<SelectionOption> Options { get; set; } = [];
    public ICollection<SelectionGroupBranchAvailability> BranchAvailability { get; set; } = [];
    public ICollection<ProductSelectionGroup> Products { get; set; } = [];
}

public sealed class SelectionOption
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SelectionGroupId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal PriceAdjustment { get; set; }
    public bool IsDefault { get; set; }
    public int MaxQuantity { get; set; } = 1;
    public int SortOrder { get; set; }
}

public sealed class SelectionGroupBranchAvailability
{
    public Guid SelectionGroupId { get; set; }
    public Guid BranchId { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public sealed class ProductSelectionGroup
{
    public Guid ProductId { get; set; }
    public Guid SelectionGroupId { get; set; }
    public SelectionGroup? SelectionGroup { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ProductImage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public required string Url { get; set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ProductBranchAvailability
{
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public sealed class TaxCategory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PreparationStation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class CatalogRules
{
    public const int NameMax = 160;
    public const int DescriptionMax = 2000;
    public const int SkuMax = 64;
    public const int BarcodeMax = 64;
    public const int ImageUrlMax = 500;
    public const int CodeMax = 50;
    public const int SortOrderMin = 0;
    public const int SortOrderMax = 9999;
    public const int PricePrecision = 4;
    public const int SelectionMax = 99;

    public static bool ValidSortOrder(int? value) => value is not null && value is >= SortOrderMin and <= SortOrderMax;
    public static bool ValidSku(string sku) => !string.IsNullOrWhiteSpace(sku) && sku.Trim().Length <= SkuMax;
    public static bool ValidBarcode(string? barcode) => string.IsNullOrWhiteSpace(barcode) || barcode.Trim().Length <= BarcodeMax;
    public static bool ValidUrl(string? url) => string.IsNullOrWhiteSpace(url) || url.Trim().Length <= ImageUrlMax;
    public static bool ValidNonNegativePrice(decimal? value) => value is null or >= 0;
    public static bool ValidSelectionRange(bool isRequired, int min, int max) => min >= (isRequired ? 1 : 0) && min <= max && max <= SelectionMax;

    public static SelectionCalculation Calculate(SelectionGroup group, IEnumerable<SelectionChoice> selections)
    {
        var requested = selections.GroupBy(x => x.OptionId).Select(x => new SelectionChoice(x.Key, x.Sum(y => y.Quantity))).ToList();
        if (requested.Any(x => x.Quantity < 1)) return SelectionCalculation.Invalid("Selection quantities must be positive.");
        var options = group.Options.ToDictionary(x => x.Id);
        if (requested.Any(x => !options.ContainsKey(x.OptionId))) return SelectionCalculation.Invalid("One or more selected options do not belong to this group.");
        if (requested.Any(x => x.Quantity > options[x.OptionId].MaxQuantity)) return SelectionCalculation.Invalid("An option exceeds its quantity limit.");
        var count = requested.Sum(x => x.Quantity);
        if (count < group.MinSelections || count > group.MaxSelections) return SelectionCalculation.Invalid($"Select between {group.MinSelections} and {group.MaxSelections} options.");
        return new SelectionCalculation(true, null, requested.Sum(x => options[x.OptionId].PriceAdjustment * x.Quantity));
    }
}

public sealed record SelectionChoice(Guid OptionId, int Quantity);
public sealed record SelectionCalculation(bool IsValid, string? Error, decimal PriceAdjustment)
{
    public static SelectionCalculation Invalid(string error) => new(false, error, 0m);
}
