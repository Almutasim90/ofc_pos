namespace OFC.Modules.Sync;

public enum SyncOperationStatus
{
    Received = 0,
    Applied = 1,
    Duplicate = 2,
    Conflict = 3,
    Failed = 4
}

public sealed class SyncOperation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public long? BaseVersion { get; set; }
    public int? BaseCatalogVersion { get; set; }
    public SyncOperationStatus Status { get; set; } = SyncOperationStatus.Received;
    public string? Result { get; set; }
    public string? ConflictReason { get; set; }
    public string? Error { get; set; }
    public long? ServerVersion { get; set; }
    public DateTimeOffset? ClientOccurredAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SyncState
{
    public Guid BranchId { get; set; }
    public long CurrentVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class SyncRules
{
    public const int OperationTypeMax = 40;
    public const int PayloadMax = 60000;
    public const int BatchMax = 200;
    public const string ConflictReasonStalePricing = "stale-pricing";
    public const string ConflictReasonNegativeStock = "negative-stock";
    public const string ConflictReasonEntityConflict = "entity-conflict";

    public static readonly string[] KnownOperationTypes = ["order.create", "inventory.movement.post"];

    public static bool ValidIdempotencyKey(Guid key) => key != Guid.Empty;

    public static bool ValidOperationType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && type!.Trim().Length <= OperationTypeMax && KnownOperationTypes.Contains(type!.Trim());

    public static bool IsKnownOperationType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && KnownOperationTypes.Contains(type!.Trim());

    public static bool ValidBatchSize(int count) => count is > 0 and <= BatchMax;

    // The client computed its prices against an older catalog snapshot than the server currently publishes.
    public static bool IsStaleCatalog(int? lineCatalogVersion, int currentCatalogVersion) =>
        currentCatalogVersion > 0 && lineCatalogVersion.HasValue && lineCatalogVersion.Value < currentCatalogVersion;

    // The device's sync stream lags the branch's server-backed version.
    public static bool IsVersionBehind(long currentVersion, long? baseVersion) =>
        baseVersion.HasValue && baseVersion.Value < currentVersion;

    // A previously recorded operation may be re-applied only while it is not settled.
    public static bool CanApply(SyncOperationStatus status) =>
        status is SyncOperationStatus.Received or SyncOperationStatus.Failed;

    public static bool IsSettled(SyncOperationStatus status) =>
        status is SyncOperationStatus.Applied or SyncOperationStatus.Duplicate or SyncOperationStatus.Conflict;

    public static string RequiredPermission(string? operationType) => operationType switch
    {
        "order.create" => "orders.manage",
        "inventory.movement.post" => "inventory.movements.manage",
        _ => string.Empty
    };
}
