namespace OFC.Modules.Integrations;

public enum IntegrationKind
{
    Loyalty = 0,
    CustomerCrm = 1,
    OnlineOrdering = 2,
    Delivery = 3,
    Notifications = 4,
    AiAssist = 5,
    ExternalBilling = 6
}

public enum IntegrationChannel
{
    Webhook = 0,
    Email = 1,
    Sms = 2
}

public enum OutboxStatus
{
    Queued = 0,
    Dispatching = 1,
    Dispatched = 2,
    Failed = 3,
    Skipped = 4,
    Deferred = 5
}

public enum AiSuggestionKind
{
    Upsell = 0,
    DemandForecast = 1,
    StockForecast = 2,
    Insight = 3
}

public sealed class ExternalOutboxEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? BranchId { get; set; }
    public IntegrationKind Kind { get; set; }
    public IntegrationChannel Channel { get; set; } = IntegrationChannel.Webhook;
    public required string Type { get; set; }
    public string Payload { get; set; } = "{}";
    public OutboxStatus Status { get; set; } = OutboxStatus.Queued;
    public string CorrelationId { get; set; } = string.Empty;
    public required string IdempotencyKey { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = IntegrationRules.MaxAttempts;
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAt { get; set; }
}

public sealed record IntegrationPreference(IntegrationKind Kind, bool Enabled);

public sealed record OutboxMessage(string CorrelationId, IntegrationKind Kind, IntegrationChannel Channel, string Type, string Payload);

public sealed record AdapterResult(bool Success, string Detail);

public interface IExternalIntegrationAdapter
{
    IntegrationKind Kind { get; }
    string Name { get; }
    Task<AdapterResult> TestAsync(CancellationToken ct);
    Task<AdapterResult> DispatchAsync(OutboxMessage message, CancellationToken ct);
}

public sealed record AiAssistRequest(Guid BranchId, string Context, string? Currency, string? Language);

public sealed record AiSuggestion(AiSuggestionKind Kind, string TitleAr, string TitleEn, string? Detail, decimal Confidence, bool RequiresReview = true);

public sealed record AiAssistResult(IReadOnlyList<AiSuggestion> Suggestions, string Mode, string? Error, bool SafeFallback = false);

public interface IAiAssistProvider
{
    Task<AiAssistResult> SuggestAsync(AiAssistRequest request, CancellationToken ct);
}
