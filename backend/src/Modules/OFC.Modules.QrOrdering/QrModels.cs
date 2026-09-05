namespace OFC.Modules.QrOrdering;

public enum QrContextKind { Table = 0, Parking = 1, Branch = 2 }
public enum QrApprovalMode { None = 0, AutoApprove = 1, RequiresStaffApproval = 2 }
public enum QrOrderApprovalStatus { Pending = 0, Approved = 1, Rejected = 2 }

public sealed class QrContext
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid SalesChannelId { get; set; }
    public QrContextKind Kind { get; set; } = QrContextKind.Table;
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public QrApprovalMode ApprovalMode { get; set; } = QrApprovalMode.AutoApprove;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class QrOrderApproval
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid QrContextId { get; set; }
    public Guid? CustomerId { get; set; }
    public QrOrderApprovalStatus Status { get; set; } = QrOrderApprovalStatus.Pending;
    public string? Note { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public string? Phone { get; set; }
    public string? ExternalId { get; set; }
    public string? LoyaltyReference { get; set; }
    public bool IsWalkIn { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
