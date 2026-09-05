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

public enum InventoryCountStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2
}

public enum StockTransferStatus
{
    Draft = 0,
    InTransit = 1,
    Received = 2,
    Cancelled = 3
}

public enum WasteCategory
{
    Expired = 0,
    Damaged = 1,
    PreparationWaste = 2,
    FinishedProductWaste = 3,
    CancelledOrderWaste = 4
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

public sealed class InventoryCount
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Number { get; set; }
    public Guid BranchId { get; set; }
    public InventoryCountStatus Status { get; set; } = InventoryCountStatus.Draft;
    public string? Note { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid ClientCountId { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<InventoryCountLine> Lines { get; set; } = [];
}

public sealed class InventoryCountLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CountId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid UnitId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Variance { get; set; }
    public string? Reason { get; set; }
}

public sealed class StockTransfer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Number { get; set; }
    public Guid SourceBranchId { get; set; }
    public Guid DestinationBranchId { get; set; }
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;
    public string? Note { get; set; }
    public string? Reference { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public Guid ClientTransferId { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<StockTransferLine> Lines { get; set; } = [];
}

public sealed class StockTransferLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid StockTransferId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid UnitId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed class WasteRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Number { get; set; }
    public Guid BranchId { get; set; }
    public Guid InventoryItemId { get; set; }
    public WasteCategory Category { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Reference { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OrderLineId { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? InventoryMovementId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid ClientRecordId { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
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
    public const int NumberMax = 40;
    public const int NoteMax = 500;
    public const int PhotoUrlMax = 500;
    public const int MaxDocumentLines = 500;
    public const int MoneyPrecision = 4;

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= NameMax;
    public static bool ValidCode(string? code) => !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= CodeMax;
    public static bool ValidSymbol(string? symbol) => string.IsNullOrWhiteSpace(symbol) || symbol.Trim().Length <= SymbolMax;
    public static bool ValidSku(string sku) => !string.IsNullOrWhiteSpace(sku) && sku.Trim().Length <= SkuMax;
    public static bool ValidBarcode(string? barcode) => string.IsNullOrWhiteSpace(barcode) || barcode.Trim().Length <= BarcodeMax;
    public static bool ValidDescription(string? description) => string.IsNullOrWhiteSpace(description) || description.Trim().Length <= DescriptionMax;
    public static bool ValidReference(string? reference) => string.IsNullOrWhiteSpace(reference) || reference.Trim().Length <= ReferenceMax;
    public static bool ValidReason(string? reason) => string.IsNullOrWhiteSpace(reason) || reason.Trim().Length <= ReasonMax;
    public static bool ValidNote(string? note) => string.IsNullOrWhiteSpace(note) || note.Trim().Length <= NoteMax;
    public static bool ValidNumber(string? number) => !string.IsNullOrWhiteSpace(number) && number.Trim().Length <= NumberMax;
    public static bool ValidPhotoUrl(string? url) => string.IsNullOrWhiteSpace(url) || url.Trim().Length <= PhotoUrlMax;
    public static bool ValidCost(decimal cost) => cost >= 0m;
    public static bool ValidRecipeLineCount(int count) => count is > 0 and <= MaxRecipeLines;
    public static bool ValidDocumentLineCount(int count) => count is > 0 and <= MaxDocumentLines;
    public static bool ValidCountedQuantity(decimal quantity) => quantity >= 0m && quantity <= 9999999m;
    public static bool ValidWasteQuantity(decimal quantity) => quantity >= MinAbsoluteQuantity && quantity <= 9999m;

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
    public static decimal RoundCost(decimal cost) => decimal.Round(cost, CostPrecision, MidpointRounding.AwayFromZero);
    public static decimal RoundMoney(decimal value) => decimal.Round(value, MoneyPrecision, MidpointRounding.AwayFromZero);
    public static decimal CountVariance(decimal systemQuantity, decimal countedQuantity) => RoundQuantity(countedQuantity - systemQuantity);
    public static bool IsSignificantVariance(decimal variance) => Math.Abs(variance) >= MinAbsoluteQuantity;

    public static bool CanApproveCount(InventoryCountStatus status) => status == InventoryCountStatus.Draft;
    public static bool CanPostCount(InventoryCountStatus status) => status == InventoryCountStatus.Draft;
    public static bool CanCancelCount(InventoryCountStatus status) => status == InventoryCountStatus.Draft;
    public static bool CanShipTransfer(StockTransferStatus status) => status == StockTransferStatus.Draft;
    public static bool CanReceiveTransfer(StockTransferStatus status) => status == StockTransferStatus.InTransit;
    public static bool CanCancelTransfer(StockTransferStatus status) => status is StockTransferStatus.Draft or StockTransferStatus.InTransit;

    public static bool TryComputeRecipeCost(IReadOnlyList<RecipeLine> lines, IReadOnlyDictionary<Guid, InventoryItem> items, IReadOnlyList<UnitConversion> conversions, out decimal totalCost)
    {
        totalCost = 0m;
        if (lines.Count == 0) return false;
        foreach (var line in lines)
        {
            if (line.Quantity < 0m || !items.TryGetValue(line.InventoryItemId, out var item) || !ValidCost(item.UnitCost)) return false;
            var basePerLineUnit = 1m;
            if (line.UnitId != item.BaseUnitId && !TryConvert(1m, line.UnitId, item.BaseUnitId, conversions, out basePerLineUnit)) return false;
            totalCost += line.Quantity * item.UnitCost * basePerLineUnit;
        }
        totalCost = RoundMoney(totalCost);
        return true;
    }

    public static decimal FoodCostPercent(decimal recipeCost, decimal sellingPrice) => sellingPrice == 0m ? 0m : RoundMoney(recipeCost / sellingPrice * 100m);
    public static decimal GrossMargin(decimal recipeCost, decimal sellingPrice) => RoundMoney(sellingPrice - recipeCost);
    public static decimal GrossMarginPercent(decimal recipeCost, decimal sellingPrice) => sellingPrice == 0m ? 0m : RoundMoney((sellingPrice - recipeCost) / sellingPrice * 100m);
}
