namespace OFC.Modules.Payments;

public enum PaymentMethodKind { Cash = 0, OmanNet = 1, Visa = 2, Mastercard = 3, ApplePay = 4, Voucher = 5, Online = 6, Other = 7 }
public enum PaymentStatus { Pending = 0, Authorized = 1, Captured = 2, Failed = 3, Cancelled = 4, Reversed = 5 }
public enum FinancialTransactionType { Sale = 0, Refund = 1, Reversal = 2 }

public sealed class PaymentMethod
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public PaymentMethodKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid BranchId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public Guid ClientRequestId { get; set; }
    public decimal Amount { get; set; }
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ProviderReference { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<PaymentStatusHistory> StatusHistory { get; set; } = [];
}

public sealed class PaymentStatusHistory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PaymentId { get; set; }
    public PaymentStatus FromStatus { get; set; }
    public PaymentStatus ToStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
}

// Financial records are append-only; refunds and reversals reference the original record in later sprints.
public sealed class FinancialTransaction
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid BranchId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? ShiftId { get; set; }
    public FinancialTransactionType Type { get; set; } = FinancialTransactionType.Sale;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ReversalReferenceId { get; set; }
}

public static class PaymentRules
{
    public const int CodeMax = 40;
    public const int ReferenceMax = 120;
    public const decimal MoneyTolerance = 0.0001m;
    public static decimal RoundMoney(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    public static bool IsCash(PaymentMethodKind kind) => kind == PaymentMethodKind.Cash;
    public static bool CanTransition(PaymentStatus from, PaymentStatus to) => from == to || (from, to) switch
    {
        (PaymentStatus.Pending, PaymentStatus.Authorized or PaymentStatus.Captured or PaymentStatus.Failed or PaymentStatus.Cancelled) => true,
        (PaymentStatus.Authorized, PaymentStatus.Captured or PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Reversed) => true,
        (PaymentStatus.Captured, PaymentStatus.Reversed) => true,
        _ => false
    };
}
