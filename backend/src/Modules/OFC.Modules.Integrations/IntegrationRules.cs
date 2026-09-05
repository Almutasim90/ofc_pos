namespace OFC.Modules.Integrations;

public static class IntegrationRules
{
    public const string PreferencePrefix = "integration.";
    public const string AiAssistPreferenceKey = "ai.assist.enabled";
    public const int MaxOutboxTypeLength = 80;
    public const int MaxOutboxPayloadLength = 16000;
    public const int MaxCorrelationIdLength = 100;
    public const int MaxErrorLength = 1000;
    public const int MaxAttempts = 5;
    public const int RetryBaseDelaySeconds = 30;
    public const int MaxListItems = 100;

    public static string PreferenceKey(IntegrationKind kind) => $"{PreferencePrefix}{kind.ToString().ToLowerInvariant()}.enabled";
    public static string PreferenceKeyFor(IntegrationKind kind) => kind == IntegrationKind.AiAssist ? AiAssistPreferenceKey : PreferenceKey(kind);

    public static bool ParseEnabled(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    public static string EnabledValue(bool enabled) => enabled ? "true" : "false";

    public static bool IsKnownKind(IntegrationKind kind) => Enum.IsDefined(kind);
    public static bool ValidChannel(IntegrationChannel channel) => Enum.IsDefined(channel);

    public static bool ValidType(string? type) => !string.IsNullOrWhiteSpace(type) && type.Trim().Length <= MaxOutboxTypeLength;
    public static bool ValidPayload(string? payload) => !string.IsNullOrWhiteSpace(payload) && payload.Trim().Length <= MaxOutboxPayloadLength;
    public static bool ValidCorrelationId(string? correlationId) => string.IsNullOrWhiteSpace(correlationId) || correlationId.Trim().Length <= MaxCorrelationIdLength;
    public static bool ValidIdempotencyKey(string? key) => !string.IsNullOrWhiteSpace(key) && key.Trim().Length <= 100;

    public static bool IsSensitive(IntegrationKind kind) => kind is IntegrationKind.Notifications or IntegrationKind.Delivery;
    public static bool RequiresReview(IntegrationKind kind) => kind is IntegrationKind.Loyalty or IntegrationKind.CustomerCrm or IntegrationKind.OnlineOrdering;

    public static bool SuggestionAcceptsWithoutReview(AiSuggestion suggestion) => false;
    public static bool IsSafeAiResult(AiAssistResult result) => result.SafeFallback || result.Suggestions.All(suggestion => !SuggestionAcceptsWithoutReview(suggestion));

    public static OutboxStatus NextStatusAfterFailure(ExternalOutboxEntry entry) => entry.Attempts >= entry.MaxAttempts ? OutboxStatus.Failed : OutboxStatus.Deferred;
    public static int RetryDelaySeconds(int attempt) => RetryBaseDelaySeconds * (int)Math.Pow(2, Math.Max(attempt - 1, 0));

    public static bool CanAttempt(ExternalOutboxEntry entry, DateTimeOffset now) =>
        entry.Attempts < entry.MaxAttempts
        && entry.Status is OutboxStatus.Queued or OutboxStatus.Deferred or OutboxStatus.Failed
        && (entry.NextAttemptAt is null || entry.NextAttemptAt <= now);

    public static (string NameAr, string NameEn) DisplayName(IntegrationKind kind) => kind switch
    {
        IntegrationKind.Loyalty => ("برنامج الولاء", "Loyalty"),
        IntegrationKind.CustomerCrm => ("إدارة العملاء", "Customer CRM"),
        IntegrationKind.OnlineOrdering => ("الطلب الإلكتروني", "Online ordering"),
        IntegrationKind.Delivery => ("التوصيل الخارجي", "Delivery adapters"),
        IntegrationKind.Notifications => ("الإشعارات", "Notifications"),
        IntegrationKind.AiAssist => ("مساعد الذكاء الاصطناعي", "AI assist"),
        IntegrationKind.ExternalBilling => ("الفوترة الخارجية", "External billing"),
        _ => (kind.ToString(), kind.ToString())
    };

    public static IntegrationKind[] DefaultKinds { get; } =
    [
        IntegrationKind.Loyalty,
        IntegrationKind.CustomerCrm,
        IntegrationKind.OnlineOrdering,
        IntegrationKind.Delivery,
        IntegrationKind.Notifications,
        IntegrationKind.AiAssist,
        IntegrationKind.ExternalBilling
    ];
}
