namespace OFC.Modules.Ordering;

public enum CancellationOperation { Void = 0, Cancel = 1, Refund = 2 }

public sealed class CancellationReason
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public bool RequiresNote { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CancellationApprovalThreshold
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public CancellationOperation Operation { get; set; }
    public decimal Amount { get; set; }
}

public sealed class OrderCancellation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid CancellationReasonId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CancelledByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? ShiftId { get; set; }
    public string? Note { get; set; }
    public decimal OrderTotal { get; set; }
    public OrderStatus OrderStatusAtCancellation { get; set; }
    public bool WasSentToKitchen { get; set; }
    public bool ReturnInventory { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset CancelledAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OrderLineVoid
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public Guid CancellationReasonId { get; set; }
    public Guid BranchId { get; set; }
    public Guid VoidedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset VoidedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Refund
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid CancellationReasonId { get; set; }
    public Guid BranchId { get; set; }
    public Guid RefundedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    // Inventory movement is intentionally deferred to Sprint 15.
    public bool ReturnInventory { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset RefundedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class CancellationRules
{
    public const int CodeMax = 40;
    public const int NameMax = 160;
    public const int NoteMax = 500;
    public const decimal MoneyTolerance = 0.0001m;
    public static bool WasSentToKitchen(OrderStatus status) => status is OrderStatus.SentToKitchen or OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Completed;
    public static bool RequiresNote(CancellationReason reason) => reason.RequiresNote || string.Equals(reason.Code, "OTHER", StringComparison.OrdinalIgnoreCase);
}
