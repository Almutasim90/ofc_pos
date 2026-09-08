namespace OFC.Modules.Printing;

public enum PrinterKind { Receipt = 0, Kitchen = 1, CashDrawer = 2 }
public enum PrintJobKind { Receipt = 0, Kitchen = 1, CashDrawerOpen = 2 }
public enum PrintJobStatus { Pending = 0, Printing = 1, Printed = 2, Failed = 3, Retrying = 4 }

public sealed class PrinterConfiguration
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public PrinterKind Kind { get; set; }
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    // Reported by the Local Print Agent polling this printer's job queue (see OFC.PrintAgent). Absent or
    // stale (older than PrintingRules.HealthStaleAfter) means no agent is currently watching this printer.
    public DateTimeOffset? LastSeenAt { get; set; }
    public string? LastHealthError { get; set; }
}

public sealed class PrintTemplate
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public required string Code { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public PrinterKind Kind { get; set; }
    public int WidthChars { get; set; } = 42;
    public required string Content { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PrinterRoute
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid? PreparationStationId { get; set; }
    public Guid PrinterConfigurationId { get; set; }
    public Guid PrintTemplateId { get; set; }
    public int Priority { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PrintJob
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid ClientRequestId { get; set; }
    public PrintJobKind Kind { get; set; }
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;
    public Guid? PrinterConfigurationId { get; set; }
    public string? TemplateCode { get; set; }
    public string Payload { get; set; } = "{}";
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? PrintedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PrintingRules
{
    public const int CodeMax = 50;
    public const int NameMax = 160;
    public const int DeviceNameMax = 100;
    public const int TemplateContentMax = 4000;
    public const int JobErrorMax = 500;
    public const int TemplateWidthMin = 20;
    public const int TemplateWidthMax = 120;
    public const int PriorityMax = 999;
    public const int SortOrderMax = 9999;
    public const int RouteCountMax = 200;
    public const int JobCountMax = 500;
    public static readonly TimeSpan HealthStaleAfter = TimeSpan.FromSeconds(20);
    public static bool IsHealthy(DateTimeOffset? lastSeenAt, DateTimeOffset now) => lastSeenAt.HasValue && now - lastSeenAt.Value <= HealthStaleAfter;

    public static bool CanTransition(PrintJobStatus from, PrintJobStatus to) => from == to || (from, to) switch
    {
        (PrintJobStatus.Pending, PrintJobStatus.Printing or PrintJobStatus.Failed) => true,
        (PrintJobStatus.Retrying, PrintJobStatus.Printing) => true,
        (PrintJobStatus.Printing, PrintJobStatus.Printed or PrintJobStatus.Failed or PrintJobStatus.Retrying) => true,
        (PrintJobStatus.Failed, PrintJobStatus.Retrying) => true,
        _ => false
    };

    public static bool ShouldRetry(PrintJob job, DateTimeOffset now)
    {
        if (job.Status is not (PrintJobStatus.Failed or PrintJobStatus.Retrying)) return false;
        if (job.AttemptCount >= job.MaxAttempts) return false;
        return job.NextAttemptAt is null || now >= job.NextAttemptAt.Value;
    }

    public static TimeSpan RetryBackoff(int attemptCount) => attemptCount switch
    {
        <= 1 => TimeSpan.FromSeconds(5),
        2 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromMinutes(2)
    };

    public static PrinterRoute? PickRoute(IEnumerable<PrinterRoute> routes, Guid? preparationStationId)
    {
        var active = routes.Where(x => x.IsActive).ToList();
        return active.Where(x => x.PreparationStationId == preparationStationId)
                .OrderBy(x => x.Priority).ThenByDescending(x => x.CreatedAt).FirstOrDefault()
            ?? active.Where(x => x.PreparationStationId is null)
                .OrderBy(x => x.Priority).ThenByDescending(x => x.CreatedAt).FirstOrDefault();
    }

    public static bool ValidWidth(int width) => width is >= TemplateWidthMin and <= TemplateWidthMax;
}
