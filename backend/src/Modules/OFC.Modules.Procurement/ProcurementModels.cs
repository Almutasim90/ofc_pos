namespace OFC.Modules.Procurement;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Received = 5
}

public enum GoodsReceiptStatus
{
    Draft = 0,
    Posted = 1
}

public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? VatNumber { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Number { get; set; }
    public Guid SupplierId { get; set; }
    public Guid BranchId { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTimeOffset? ExpectedDate { get; set; }
    public string? Notes { get; set; }
    public string? Reference { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<PurchaseOrderLine> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PurchaseOrderId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedQuantity { get; set; }
}

public sealed class GoodsReceipt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Number { get; set; }
    public Guid SupplierId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid ClientReceiptId { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<GoodsReceiptLine> Lines { get; set; } = [];
}

public sealed class GoodsReceiptLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GoodsReceiptId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalAmount { get; set; }
}

public static class ProcurementRules
{
    public const int CodeMax = 50;
    public const int NameMax = 160;
    public const int ContactMax = 160;
    public const int PhoneMax = 50;
    public const int EmailMax = 320;
    public const int VatMax = 50;
    public const int AddressMax = 500;
    public const int NotesMax = 2000;
    public const int ReferenceMax = 200;
    public const int NumberMax = 40;
    public const int QuantityPrecision = 6;
    public const int CostPrecision = 6;
    public const int MoneyPrecision = 4;
    public const int MaxLines = 500;
    public const decimal MinQuantity = 0.000001m;

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= NameMax;
    public static bool ValidCode(string? code) => !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= CodeMax;
    public static bool ValidContact(string? contact) => string.IsNullOrWhiteSpace(contact) || contact.Trim().Length <= ContactMax;
    public static bool ValidPhone(string? phone) => string.IsNullOrWhiteSpace(phone) || phone.Trim().Length <= PhoneMax;
    public static bool ValidEmail(string? email) => string.IsNullOrWhiteSpace(email) || (email.Trim().Length <= EmailMax && email.Contains('@'));
    public static bool ValidVat(string? vat) => string.IsNullOrWhiteSpace(vat) || vat.Trim().Length <= VatMax;
    public static bool ValidAddress(string? address) => string.IsNullOrWhiteSpace(address) || address.Trim().Length <= AddressMax;
    public static bool ValidNotes(string? notes) => string.IsNullOrWhiteSpace(notes) || notes.Trim().Length <= NotesMax;
    public static bool ValidReference(string? reference) => string.IsNullOrWhiteSpace(reference) || reference.Trim().Length <= ReferenceMax;
    public static bool ValidNumber(string? number) => !string.IsNullOrWhiteSpace(number) && number.Trim().Length <= NumberMax;
    public static bool ValidCost(decimal cost) => cost >= 0m;
    public static bool ValidQuantity(decimal quantity) => quantity >= MinQuantity && quantity <= 9999999m;
    public static bool ValidLineCount(int count) => count is > 0 and <= MaxLines;

    public static decimal RoundQuantity(decimal quantity) => decimal.Round(quantity, QuantityPrecision, MidpointRounding.AwayFromZero);
    public static decimal RoundCost(decimal cost) => decimal.Round(cost, CostPrecision, MidpointRounding.AwayFromZero);
    public static decimal RoundMoney(decimal value) => decimal.Round(value, MoneyPrecision, MidpointRounding.AwayFromZero);
    public static decimal LineTotal(decimal quantity, decimal unitCost) => RoundMoney(quantity * unitCost);

    public static decimal WeightedAverageCost(decimal currentStock, decimal currentCost, decimal receivedQuantity, decimal receivedCost)
    {
        var newStock = RoundQuantity(currentStock + receivedQuantity);
        if (newStock == 0m) return RoundCost(receivedCost);
        var numerator = (currentStock * currentCost) + (receivedQuantity * receivedCost);
        return RoundCost(numerator / newStock);
    }

    public static bool CanSubmit(PurchaseOrderStatus status) => status == PurchaseOrderStatus.Draft;
    public static bool CanApprove(PurchaseOrderStatus status) => status == PurchaseOrderStatus.Submitted;
    public static bool CanReceive(PurchaseOrderStatus status) => status == PurchaseOrderStatus.Approved;
    public static bool CanCancel(PurchaseOrderStatus status) => status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Approved;
    public static bool CanPost(GoodsReceiptStatus status) => status == GoodsReceiptStatus.Draft;
}
