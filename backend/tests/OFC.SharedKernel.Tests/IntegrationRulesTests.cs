using OFC.Modules.Integrations;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class IntegrationRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Preference_key_is_stable_and_ai_uses_dedicated_key()
    {
        Assert.Equal("integration.loyalty.enabled", IntegrationRules.PreferenceKey(IntegrationKind.Loyalty));
        Assert.Equal("integration.notifications.enabled", IntegrationRules.PreferenceKey(IntegrationKind.Notifications));
        Assert.Equal("ai.assist.enabled", IntegrationRules.PreferenceKeyFor(IntegrationKind.AiAssist));
        Assert.Equal("integration.delivery.enabled", IntegrationRules.PreferenceKeyFor(IntegrationKind.Delivery));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1", false)]
    public void Enabled_value_parsing_matches_the_stored_convention(string? value, bool expected) => Assert.Equal(expected, IntegrationRules.ParseEnabled(value));

    [Fact]
    public void Enabled_value_round_trips_through_the_written_value()
    {
        Assert.Equal("true", IntegrationRules.EnabledValue(true));
        Assert.Equal("false", IntegrationRules.EnabledValue(false));
        Assert.True(IntegrationRules.ParseEnabled(IntegrationRules.EnabledValue(true)));
        Assert.False(IntegrationRules.ParseEnabled(IntegrationRules.EnabledValue(false)));
    }

    [Fact]
    public void Known_kinds_and_channels_cover_the_documented_extension_points()
    {
        Assert.All(IntegrationRules.DefaultKinds, kind => Assert.True(IntegrationRules.IsKnownKind(kind)));
        Assert.True(IntegrationRules.ValidChannel(IntegrationChannel.Webhook));
        Assert.True(IntegrationRules.ValidChannel(IntegrationChannel.Email));
        Assert.True(IntegrationRules.ValidChannel(IntegrationChannel.Sms));
        Assert.False(IntegrationRules.IsKnownKind((IntegrationKind)999));
        Assert.False(IntegrationRules.ValidChannel((IntegrationChannel)999));
    }

    [Theory]
    [InlineData("order.completed", true)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    public void Outbox_type_is_validated(string? type, bool expected) => Assert.Equal(expected, IntegrationRules.ValidType(type));

    [Fact]
    public void Outbox_type_is_bounded_by_the_maximum_length()
    {
        Assert.True(IntegrationRules.ValidType(new string('a', IntegrationRules.MaxOutboxTypeLength)));
        Assert.False(IntegrationRules.ValidType(new string('a', IntegrationRules.MaxOutboxTypeLength + 1)));
    }

    [Theory]
    [InlineData("{\"total\":2.7}", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Outbox_payload_is_validated(string? payload, bool expected) => Assert.Equal(expected, IntegrationRules.ValidPayload(payload));

    [Fact]
    public void Outbox_payload_is_bounded_by_the_maximum_length()
    {
        Assert.True(IntegrationRules.ValidPayload(new string('a', IntegrationRules.MaxOutboxPayloadLength)));
        Assert.False(IntegrationRules.ValidPayload(new string('a', IntegrationRules.MaxOutboxPayloadLength + 1)));
    }

    [Theory]
    [InlineData("order-123", true)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    public void Idempotency_key_is_required_and_bounded(string? key, bool expected) => Assert.Equal(expected, IntegrationRules.ValidIdempotencyKey(key));

    [Fact]
    public void Correlation_id_is_optional_but_bounded()
    {
        Assert.True(IntegrationRules.ValidCorrelationId(null));
        Assert.True(IntegrationRules.ValidCorrelationId("trace-1"));
        Assert.False(IntegrationRules.ValidCorrelationId(new string('x', IntegrationRules.MaxCorrelationIdLength + 1)));
    }

    [Fact]
    public void Failure_turns_deferred_until_a_max_attempts_is_reached()
    {
        var entry = Entry();
        entry.Attempts = 2;
        entry.MaxAttempts = IntegrationRules.MaxAttempts;
        Assert.Equal(OutboxStatus.Deferred, IntegrationRules.NextStatusAfterFailure(entry));
        entry.Attempts = IntegrationRules.MaxAttempts;
        Assert.Equal(OutboxStatus.Failed, IntegrationRules.NextStatusAfterFailure(entry));
    }

    [Fact]
    public void Retry_delay_backs_off_exponentially()
    {
        Assert.Equal(30, IntegrationRules.RetryDelaySeconds(1));
        Assert.Equal(60, IntegrationRules.RetryDelaySeconds(2));
        Assert.Equal(120, IntegrationRules.RetryDelaySeconds(3));
        Assert.Equal(480, IntegrationRules.RetryDelaySeconds(5));
    }

    [Fact]
    public void Can_attempt_respects_attempt_limit_and_next_attempt_clock()
    {
        Assert.True(IntegrationRules.CanAttempt(Entry(), Now));

        var deferred = Entry();
        deferred.Status = OutboxStatus.Deferred;
        Assert.True(IntegrationRules.CanAttempt(deferred, Now));

        var gated = Entry();
        gated.NextAttemptAt = Now.AddSeconds(30);
        Assert.False(IntegrationRules.CanAttempt(gated, Now));
        Assert.True(IntegrationRules.CanAttempt(gated, Now.AddSeconds(31)));

        var exhausted = Entry();
        exhausted.Attempts = exhausted.MaxAttempts;
        Assert.False(IntegrationRules.CanAttempt(exhausted, Now));
    }

    [Fact]
    public void Ai_suggestions_never_apply_without_human_review()
    {
        var suggestion = new AiSuggestion(AiSuggestionKind.Upsell, "عرض", "Offer", "Detail", 0.9m);
        Assert.False(IntegrationRules.SuggestionAcceptsWithoutReview(suggestion));
        Assert.True(IntegrationRules.IsSafeAiResult(new AiAssistResult([suggestion], "stub", null)));
        Assert.True(IntegrationRules.IsSafeAiResult(new AiAssistResult([], "degraded", "down", SafeFallback: true)));
    }

    [Fact]
    public void Sensitive_kinds_are_audited_and_consumer_facing_kinds_require_review()
    {
        Assert.True(IntegrationRules.IsSensitive(IntegrationKind.Notifications));
        Assert.True(IntegrationRules.IsSensitive(IntegrationKind.Delivery));
        Assert.False(IntegrationRules.IsSensitive(IntegrationKind.Loyalty));
        Assert.True(IntegrationRules.RequiresReview(IntegrationKind.Loyalty));
        Assert.True(IntegrationRules.RequiresReview(IntegrationKind.CustomerCrm));
        Assert.False(IntegrationRules.RequiresReview(IntegrationKind.Notifications));
    }

    [Fact]
    public void Display_names_are_bilingual_for_every_known_kind()
    {
        foreach (var kind in IntegrationRules.DefaultKinds)
        {
            var (nameAr, nameEn) = IntegrationRules.DisplayName(kind);
            Assert.False(string.IsNullOrWhiteSpace(nameAr));
            Assert.False(string.IsNullOrWhiteSpace(nameEn));
        }
    }

    private static ExternalOutboxEntry Entry() => new() { Type = "order.completed", IdempotencyKey = "key-1", Status = OutboxStatus.Queued };
}
