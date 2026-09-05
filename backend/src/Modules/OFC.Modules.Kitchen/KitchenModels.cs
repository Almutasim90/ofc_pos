namespace OFC.Modules.Kitchen;

public enum KitchenDispatchStatus
{
    Pending = 0,
    SentToKds = 1,
    KdsAcknowledged = 2,
    PrintFallbackPending = 3,
    PrintedFallback = 4,
    Failed = 5,
    Cancelled = 6
}

public enum KitchenExecutionChannel
{
    Kds = 0,
    PrintFallback = 1,
    ManualFallback = 2
}

public enum KitchenTicketStatus
{
    New = 0,
    Preparing = 1,
    Ready = 2,
    Completed = 3,
    Cancelled = 4
}

public enum KitchenItemStatus
{
    New = 0,
    Preparing = 1,
    Ready = 2,
    Completed = 3,
    Cancelled = 4
}

public sealed class KitchenTicket
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid OrderId { get; set; }
    public Guid DispatchId { get; set; } = Guid.CreateVersion7();
    public string OrderNumber { get; set; } = "";
    public Guid? StationId { get; set; }
    public KitchenDispatchStatus DispatchStatus { get; set; } = KitchenDispatchStatus.Pending;
    public KitchenExecutionChannel Channel { get; set; } = KitchenExecutionChannel.Kds;
    public KitchenTicketStatus Status { get; set; } = KitchenTicketStatus.New;
    public int? TargetMinutes { get; set; }
    public int KdsAttempts { get; set; }
    public bool FallbackPrinted { get; set; }
    public string? LastError { get; set; }
    public string? Note { get; set; }
    public bool WasPrepStartedBeforeCancellation { get; set; }
    public bool CancellationNotified { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? FallbackPrintedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<KitchenTicketItem> Items { get; set; } = [];
}

public sealed class KitchenTicketItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid KitchenTicketId { get; set; }
    public Guid? OrderLineId { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductNameAr { get; set; }
    public required string ProductNameEn { get; set; }
    public int Quantity { get; set; }
    public int VoidedQuantity { get; set; }
    public string? Note { get; set; }
    public string SelectionsSnapshot { get; set; } = "[]";
    public KitchenItemStatus Status { get; set; } = KitchenItemStatus.New;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class KitchenRules
{
    public const int NoteMax = 500;
    public const int OrderNumberMax = 40;
    public const int ErrorMax = 500;
    public const int DefaultTargetMinutes = 15;
    public const int TargetMinutesMin = 1;
    public const int TargetMinutesMax = 999;
    public const int KdsTimeoutSeconds = 20;
    public const int MaxKdsAttempts = 2;
    public const int MaxItemsPerTicket = 200;
    public const int MaxActiveTicketsPerStation = 100;

    public static bool CanDispatchTransition(KitchenDispatchStatus from, KitchenDispatchStatus to) => from == to || (from, to) switch
    {
        (KitchenDispatchStatus.Pending, KitchenDispatchStatus.SentToKds or KitchenDispatchStatus.PrintFallbackPending or KitchenDispatchStatus.Cancelled) => true,
        (KitchenDispatchStatus.SentToKds, KitchenDispatchStatus.KdsAcknowledged or KitchenDispatchStatus.PrintFallbackPending or KitchenDispatchStatus.Failed or KitchenDispatchStatus.Cancelled) => true,
        (KitchenDispatchStatus.KdsAcknowledged, KitchenDispatchStatus.KdsAcknowledged or KitchenDispatchStatus.Cancelled) => true,
        (KitchenDispatchStatus.PrintFallbackPending, KitchenDispatchStatus.PrintedFallback or KitchenDispatchStatus.Failed or KitchenDispatchStatus.Cancelled) => true,
        (KitchenDispatchStatus.PrintedFallback, KitchenDispatchStatus.PrintedFallback or KitchenDispatchStatus.Cancelled) => true,
        (KitchenDispatchStatus.Failed, KitchenDispatchStatus.Failed or KitchenDispatchStatus.Cancelled) => true,
        _ => false
    };

    public static bool CanItemTransition(KitchenItemStatus from, KitchenItemStatus to) => from == to || (from, to) switch
    {
        (KitchenItemStatus.New, KitchenItemStatus.Preparing or KitchenItemStatus.Cancelled) => true,
        (KitchenItemStatus.Preparing, KitchenItemStatus.Ready or KitchenItemStatus.Cancelled) => true,
        (KitchenItemStatus.Ready, KitchenItemStatus.Completed or KitchenItemStatus.Cancelled) => true,
        _ => false
    };

    public static bool CanTicketTransition(KitchenTicketStatus from, KitchenTicketStatus to) => from == to || (from, to) switch
    {
        (KitchenTicketStatus.New, KitchenTicketStatus.Preparing or KitchenTicketStatus.Cancelled) => true,
        (KitchenTicketStatus.Preparing, KitchenTicketStatus.Ready or KitchenTicketStatus.Cancelled) => true,
        (KitchenTicketStatus.Ready, KitchenTicketStatus.Completed or KitchenTicketStatus.Cancelled) => true,
        _ => false
    };

    public static bool ShouldTriggerFallback(bool kdsAvailable, int kdsAttempts, DateTimeOffset createdAt, DateTimeOffset now) =>
        !kdsAvailable && (kdsAttempts >= MaxKdsAttempts || now - createdAt >= TimeSpan.FromSeconds(KdsTimeoutSeconds));

    public static bool IsOverdue(KitchenTicket ticket, DateTimeOffset now)
    {
        if (ticket.Status is KitchenTicketStatus.Completed or KitchenTicketStatus.Cancelled) return false;
        var targetMinutes = ticket.TargetMinutes ?? DefaultTargetMinutes;
        var reference = ticket.AcknowledgedAt ?? ticket.StartedAt ?? ticket.CreatedAt;
        return now - reference > TimeSpan.FromMinutes(targetMinutes);
    }

    public static bool IsActive(KitchenTicket ticket) =>
        ticket.Status is not (KitchenTicketStatus.Completed or KitchenTicketStatus.Cancelled) && ticket.DispatchStatus is not (KitchenDispatchStatus.Failed or KitchenDispatchStatus.Cancelled);

    public static bool ValidTargetMinutes(int? minutes) => minutes is null || minutes is >= TargetMinutesMin and <= TargetMinutesMax;
}
