namespace OFC.Modules.Inventory;

public enum InventoryItemType
{
    RawMaterial = 0,
    Packaging = 1,
    SemiFinished = 2,
    FinishedProduct = 3
}

public enum RecipeStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2
}

public enum InventoryMovementType
{
    Opening = 0,
    Purchase = 1,
    SaleDeduction = 2,
    Waste = 3,
    TransferIn = 4,
    TransferOut = 5,
    Adjustment = 6,
    Return = 7,
    CountAdjustment = 8
}

public sealed class UnitOfMeasure
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public string? Symbol { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UnitConversion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FromUnitId { get; set; }
    public Guid ToUnitId { get; set; }
    public decimal Factor { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InventoryItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Sku { get; set; }
    public string? Barcode { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public InventoryItemType Type { get; set; } = InventoryItemType.RawMaterial;
    public Guid BaseUnitId { get; set; }
    public UnitOfMeasure? BaseUnit { get; set; }
    public decimal UnitCost { get; set; }
    public decimal? StockOnHand { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RecipeVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public int VersionNumber { get; set; } = 1;
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<RecipeLine> Lines { get; set; } = [];
}

public sealed class RecipeLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipeVersionId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
}

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid InventoryItemId { get; set; }
    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
    public Guid? RecipeVersionId { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OrderLineId { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid ClientMovementId { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class InventoryRules
{
    public const int CodeMax = 50;
    public const int NameMax = 160;
    public const int DescriptionMax = 2000;
    public const int SkuMax = 64;
    public const int BarcodeMax = 64;
    public const int SymbolMax = 20;
    public const int ReferenceMax = 200;
    public const int ReasonMax = 500;
    public const int QuantityPrecision = 6;
    public const int CostPrecision = 6;
    public const int ConversionPrecision = 9;
    public const int MaxRecipeLines = 500;
    public const decimal MaxConversionFactor = 1_000_000_000m;
    public const decimal MinAbsoluteQuantity = 0.000001m;

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= NameMax;
    public static bool ValidCode(string? code) => !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= CodeMax;
    public static bool ValidSymbol(string? symbol) => string.IsNullOrWhiteSpace(symbol) || symbol.Trim().Length <= SymbolMax;
    public static bool ValidSku(string sku) => !string.IsNullOrWhiteSpace(sku) && sku.Trim().Length <= SkuMax;
    public static bool ValidBarcode(string? barcode) => string.IsNullOrWhiteSpace(barcode) || barcode.Trim().Length <= BarcodeMax;
    public static bool ValidDescription(string? description) => string.IsNullOrWhiteSpace(description) || description.Trim().Length <= DescriptionMax;
    public static bool ValidReference(string? reference) => string.IsNullOrWhiteSpace(reference) || reference.Trim().Length <= ReferenceMax;
    public static bool ValidReason(string? reason) => string.IsNullOrWhiteSpace(reason) || reason.Trim().Length <= ReasonMax;
    public static bool ValidCost(decimal cost) => cost >= 0m;
    public static bool ValidRecipeLineCount(int count) => count is > 0 and <= MaxRecipeLines;

    public static bool ValidConversion(UnitConversion conversion) =>
        conversion.Factor > 0m && conversion.FromUnitId != conversion.ToUnitId && conversion.Factor <= MaxConversionFactor;

    public static bool ValidDirection(InventoryMovementType type, decimal quantity)
    {
        if (quantity == 0m || Math.Abs(quantity) < MinAbsoluteQuantity) return false;
        return type switch
        {
            InventoryMovementType.Opening or InventoryMovementType.Purchase or InventoryMovementType.TransferIn or InventoryMovementType.Return => quantity > 0m,
            InventoryMovementType.TransferOut or InventoryMovementType.Waste => quantity < 0m,
            InventoryMovementType.Adjustment or InventoryMovementType.CountAdjustment => true,
            _ => false
        };
    }

    public static bool CanActivate(RecipeStatus status) => status == RecipeStatus.Draft;

    public static bool CanRevise(RecipeStatus status) => status == RecipeStatus.Active;

    public static bool TryConvert(decimal quantity, Guid fromUnitId, Guid toUnitId, IReadOnlyList<UnitConversion> conversions, out decimal result)
    {
        if (fromUnitId == toUnitId)
        {
            result = RoundQuantity(quantity);
            return true;
        }
        var adjacency = new Dictionary<Guid, List<(Guid To, decimal Factor)>>();
        foreach (var conversion in conversions.Where(x => x.IsActive && x.Factor > 0m))
        {
            if (!adjacency.TryGetValue(conversion.FromUnitId, out var forward))
            {
                forward = new List<(Guid, decimal)>();
                adjacency[conversion.FromUnitId] = forward;
            }
            forward.Add((conversion.ToUnitId, conversion.Factor));
            if (!adjacency.TryGetValue(conversion.ToUnitId, out var reverse))
            {
                reverse = new List<(Guid, decimal)>();
                adjacency[conversion.ToUnitId] = reverse;
            }
            reverse.Add((conversion.FromUnitId, 1m / conversion.Factor));
        }
        var visited = new HashSet<Guid> { fromUnitId };
        var queue = new Queue<(Guid Unit, decimal Factor)>();
        queue.Enqueue((fromUnitId, 1m));
        while (queue.Count > 0)
        {
            var (unit, factor) = queue.Dequeue();
            if (unit == toUnitId)
            {
                result = RoundQuantity(quantity * factor);
                return true;
            }
            if (!adjacency.TryGetValue(unit, out var edges)) continue;
            foreach (var (next, edgeFactor) in edges)
            {
                if (!visited.Add(next)) continue;
                queue.Enqueue((next, factor * edgeFactor));
            }
        }
        result = 0m;
        return false;
    }

    public static List<(Guid InventoryItemId, Guid UnitId, decimal Quantity)> PlanDeductions(IEnumerable<RecipeLine> lines, decimal productQuantity)
    {
        return lines
            .Where(x => x.Quantity != 0m)
            .GroupBy(x => new { x.InventoryItemId, x.UnitId })
            .Select(group => (group.Key.InventoryItemId, group.Key.UnitId, RoundQuantity(productQuantity * group.Sum(x => x.Quantity))))
            .ToList();
    }

    public static decimal RoundQuantity(decimal quantity) => decimal.Round(quantity, QuantityPrecision, MidpointRounding.AwayFromZero);
}
