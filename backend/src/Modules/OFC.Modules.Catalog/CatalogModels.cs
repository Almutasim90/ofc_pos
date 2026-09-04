namespace OFC.Modules.Catalog;

public enum ProductType
{
    Simple = 0,
    Combo = 1,
    Service = 2
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

    public static bool ValidSortOrder(int? value) => value is not null && value is >= SortOrderMin and <= SortOrderMax;
    public static bool ValidSku(string sku) => !string.IsNullOrWhiteSpace(sku) && sku.Trim().Length <= SkuMax;
    public static bool ValidBarcode(string? barcode) => string.IsNullOrWhiteSpace(barcode) || barcode.Trim().Length <= BarcodeMax;
    public static bool ValidUrl(string? url) => string.IsNullOrWhiteSpace(url) || url.Trim().Length <= ImageUrlMax;
    public static bool ValidNonNegativePrice(decimal? value) => value is null or >= 0;
}
