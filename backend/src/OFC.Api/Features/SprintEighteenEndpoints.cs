using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Integrations;
using OFC.Modules.Ordering;
using OFC.Modules.Organization;

namespace OFC.Api.Features;

public static class SprintEighteenEndpoints
{
    private static readonly OrderStatus[] SalesStatuses =
        [OrderStatus.Paid, OrderStatus.SentToKitchen, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed, OrderStatus.PartiallyRefunded, OrderStatus.Refunded];

    public static void MapSprintEighteenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/integrations");
        api.MapGet("/preferences", ListPreferences).RequireAuthorization();
        api.MapPost("/preferences", SetPreference).RequireAuthorization();
        api.MapPost("/preferences/{kind}/test", TestAdapter).RequireAuthorization();
        api.MapPost("/outbox", EnqueueOutbox).RequireAuthorization();
        api.MapGet("/outbox", ListOutbox).RequireAuthorization();
        api.MapPost("/outbox/{id:guid}/dispatch", DispatchOutbox).RequireAuthorization();
        api.MapPost("/ai/suggest", AiSuggest).RequireAuthorization();
    }

    // STORY-18-01/02/09: optional extension points & preferences.
    private static async Task<IResult> ListPreferences(Guid? branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!CanView(user)) return Forbidden();
        branchId = NormalizeBranch(user, branchId);
        if (branchId is null) return Validation("branchId", "A branch is required to list integration preferences.");
        if (!await HasAccess(db, user, branchId.Value, ct)) return Forbidden();
        var enabled = await ReadPreferenceMap(db, branchId.Value, ct);
        var now = DateTimeOffset.UtcNow;
        return Results.Ok(new
        {
            branchId,
            extensionPoints = IntegrationRules.DefaultKinds.Select(kind => PreferenceResponse(kind, enabled.TryGetValue(IntegrationRules.PreferenceKeyFor(kind), out var value) && value, IntegrationRules.DisplayName(kind), now))
        });
    }

    private static async Task<IResult> SetPreference(SetPreferenceRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!IntegrationRules.IsKnownKind(request.Kind)) return Validation("kind", "The integration kind is invalid.");
        if (!await HasAccess(db, user, request.BranchId, ct)) return Forbidden();
        var key = IntegrationRules.PreferenceKeyFor(request.Kind);
        var setting = await db.BranchSettings.SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.Key == key, ct);
        var oldValue = setting?.Value;
        if (setting is null) db.BranchSettings.Add(new BranchSetting { BranchId = request.BranchId, Key = key, Value = IntegrationRules.EnabledValue(request.Enabled) });
        else setting.Value = IntegrationRules.EnabledValue(request.Enabled);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "integration.preference.set", "branch_setting", $"{request.BranchId}:{key}", context.TraceIdentifier, JsonSerializer.Serialize(new { oldValue }), JsonSerializer.Serialize(new { request.Kind, request.Enabled, key }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(PreferenceResponse(request.Kind, request.Enabled, IntegrationRules.DisplayName(request.Kind), DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> TestAdapter(string kind, Guid? branchId, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!Enum.TryParse<IntegrationKind>(kind, ignoreCase: true, out var parsed) || !IntegrationRules.IsKnownKind(parsed)) return Validation("kind", "The integration kind is invalid.");
        branchId = NormalizeBranch(user, branchId) ?? branchId;
        if (branchId is null) return Validation("branchId", "A branch is required to test an integration adapter.");
        if (!await HasAccess(db, user, branchId.Value, ct)) return Forbidden();
        var enabled = await IsEnabled(db, branchId.Value, parsed, ct);
        var adapter = ResolveAdapter(parsed);
        AdapterResult result;
        try
        {
            result = enabled ? await adapter.TestAsync(ct) : new AdapterResult(false, "Disabled by preference.");
        }
        catch (Exception exception)
        {
            result = new AdapterResult(false, $"Adapter test failed safely: {exception.Message}");
        }
        identity.Audit(UserId(user), branchId, DeviceId(user), "integration.adapter.test", "integration", parsed.ToString(), context.TraceIdentifier, JsonSerializer.Serialize(new { enabled }), JsonSerializer.Serialize(result));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { kind = parsed.ToString(), enabled, adapter = adapter.Name, success = result.Success, detail = result.Detail });
    }

    // STORY-18-05: webhook / notification outbox foundation.
    private static async Task<IResult> EnqueueOutbox(EnqueueOutboxRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        if (!IntegrationRules.IsKnownKind(request.Kind)) return Validation("kind", "The integration kind is invalid.");
        if (!IntegrationRules.ValidChannel(request.Channel)) return Validation("channel", "The integration channel is invalid.");
        if (!IntegrationRules.ValidType(request.Type)) return Validation("type", "The notification type is invalid.");
        if (!IntegrationRules.ValidPayload(request.Payload)) return Validation("payload", "The outbox payload is invalid.");
        if (!IntegrationRules.ValidCorrelationId(request.CorrelationId)) return Validation("correlationId", "The correlation id is invalid.");
        if (!IntegrationRules.ValidIdempotencyKey(request.IdempotencyKey)) return Validation("idempotencyKey", "The idempotency key is invalid.");
        if (request.ReferenceType is { Length: > 80 }) return Validation("referenceType", "The reference type is invalid.");
        var branchId = NormalizeBranch(user, request.BranchId) ?? request.BranchId;
        if (branchId is null) return Validation("branchId", "A branch is required to enqueue an outbox message.");
        if (!await HasAccess(db, user, branchId.Value, ct)) return Forbidden();

        var existing = await db.ExternalOutboxEntries.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null) return Results.Ok(OutboxResponse(existing));

        var entry = new ExternalOutboxEntry
        {
            BranchId = branchId,
            Kind = request.Kind,
            Channel = request.Channel,
            Type = request.Type.Trim(),
            Payload = request.Payload.Trim(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = request.CorrelationId?.Trim() ?? context.TraceIdentifier,
            ReferenceType = string.IsNullOrWhiteSpace(request.ReferenceType) ? null : request.ReferenceType.Trim(),
            ReferenceId = request.ReferenceId,
            MaxAttempts = IntegrationRules.MaxAttempts,
            Status = OutboxStatus.Queued
        };
        db.ExternalOutboxEntries.Add(entry);
        identity.Audit(UserId(user), branchId, DeviceId(user), "integration.outbox.enqueue", "external_outbox", entry.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { entry.IdempotencyKey, entry.Kind, entry.Channel, entry.Type, sensitive = IntegrationRules.IsSensitive(request.Kind) }));
        try { await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/integrations/outbox/{entry.Id}", OutboxResponse(entry)); }
        catch (DbUpdateException)
        {
            var duplicate = await db.ExternalOutboxEntries.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (duplicate is not null) return Results.Ok(OutboxResponse(duplicate));
            throw;
        }
    }

    private static async Task<IResult> ListOutbox(Guid? branchId, OutboxStatus? status, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!CanView(user)) return Forbidden();
        var branchIds = await AccessibleBranches(db, user, branchId, ct);
        if (branchIds.Count == 0) return Forbidden("You have no branch access for the outbox.");
        var query = db.ExternalOutboxEntries.AsNoTracking().Where(x => branchIds.Contains(x.BranchId!.Value));
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var entries = await query.OrderByDescending(x => x.CreatedAt).Take(IntegrationRules.MaxListItems).ToListAsync(ct);
        return Results.Ok(entries.Select(OutboxResponse));
    }

    private static async Task<IResult> DispatchOutbox(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanManage(user)) return Forbidden();
        var entry = await db.ExternalOutboxEntries.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null) return Results.NotFound();
        if (entry.BranchId is not null && !await HasAccess(db, user, entry.BranchId.Value, ct)) return Forbidden();
        if (entry.Status == OutboxStatus.Dispatched) return Results.Ok(OutboxResponse(entry));

        entry.Attempts++;
        entry.LastAttemptAt = DateTimeOffset.UtcNow;
        try
        {
            var enabled = await IsEnabled(db, entry.BranchId, entry.Kind, ct);
            if (!enabled)
            {
                entry.Status = OutboxStatus.Skipped;
                entry.LastError = "Disabled by preference.";
            }
            else
            {
                var result = await ResolveAdapter(entry.Kind).DispatchAsync(new OutboxMessage(entry.CorrelationId, entry.Kind, entry.Channel, entry.Type, entry.Payload), ct);
                if (result.Success)
                {
                    entry.Status = OutboxStatus.Dispatched;
                    entry.DispatchedAt = DateTimeOffset.UtcNow;
                    entry.NextAttemptAt = null;
                    entry.LastError = null;
                }
                else
                {
                    entry.Status = IntegrationRules.NextStatusAfterFailure(entry);
                    entry.LastError = result.Detail;
                    entry.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(IntegrationRules.RetryDelaySeconds(entry.Attempts));
                }
            }
        }
        catch (Exception exception)
        {
            entry.Status = IntegrationRules.NextStatusAfterFailure(entry);
            entry.LastError = $"Dispatch failed safely: {exception.Message}";
            entry.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(IntegrationRules.RetryDelaySeconds(entry.Attempts));
        }
        identity.Audit(UserId(user), entry.BranchId, DeviceId(user), "integration.outbox.dispatch", "external_outbox", entry.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { entry.Status, entry.Attempts, entry.LastError }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(OutboxResponse(entry));
    }

    // STORY-18-06/07/08: NON-blocking AI assist extension point.
    private static async Task<IResult> AiSuggest(AiSuggestRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!CanAi(user)) return Forbidden();
        var branchId = NormalizeBranch(user, request.BranchId) ?? request.BranchId;
        if (branchId is null) return Validation("branchId", "A branch is required to request AI assistance.");
        if (!await HasAccess(db, user, branchId.Value, ct)) return Forbidden();

        if (request.Context?.Trim().Length > 200) return Validation("context", "The AI assist context is invalid.");
        var enabled = await IsEnabled(db, branchId.Value, IntegrationKind.AiAssist, ct);
        if (!enabled)
            return Results.Ok(new { branchId, available = false, mode = "disabled", suggestions = Array.Empty<object>(), safeFallback = true, note = "AI assist is disabled by preference and never posts decisions automatically." });

        try
        {
            var provider = ResolveAiProvider();
            var result = await provider.SuggestAsync(new AiAssistRequest(branchId.Value, request.Context ?? "upsell", "OMR", user.FindFirstValue("language")), ct);
            identity.Audit(UserId(user), branchId, DeviceId(user), "integration.ai.suggest", "ai_assist", "suggestion", context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { mode = result.Mode, suggestionCount = result.Suggestions.Count, safeFallback = result.SafeFallback }));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { branchId, available = true, mode = result.Mode, suggestions = result.Suggestions.Select(ToSuggestionResponse), safeFallback = result.SafeFallback, note = "AI output is advisory only and always requires human review. It never changes an order or posts a decision by itself." });
        }
        catch (Exception exception)
        {
            identity.Audit(UserId(user), branchId, DeviceId(user), "integration.ai.suggest", "ai_assist", "suggestion", context.TraceIdentifier, oldValue: null, newValue: JsonSerializer.Serialize(new { mode = "degraded", safeFallback = true }));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { branchId, available = true, mode = "degraded", suggestions = Array.Empty<object>(), safeFallback = true, error = $"AI assist unavailable; core POS/ordering unaffected. {exception.Message}" });
        }
    }

    private static object ToSuggestionResponse(AiSuggestion suggestion) => new
    {
        kind = suggestion.Kind.ToString(),
        titleAr = suggestion.TitleAr,
        titleEn = suggestion.TitleEn,
        detail = suggestion.Detail,
        confidence = suggestion.Confidence,
        requiresReview = IntegrationRules.SuggestionAcceptsWithoutReview(suggestion) is false
    };

    private static object PreferenceResponse(IntegrationKind kind, bool enabled, (string NameAr, string NameEn) name, DateTimeOffset at) => new
    {
        kind = kind.ToString(),
        preferenceKey = IntegrationRules.PreferenceKeyFor(kind),
        nameAr = name.NameAr,
        nameEn = name.NameEn,
        enabled,
        sensitive = IntegrationRules.IsSensitive(kind),
        requiresReview = IntegrationRules.RequiresReview(kind),
        at
    };

    private static object OutboxResponse(ExternalOutboxEntry entry) => new
    {
        entry.Id,
        entry.BranchId,
        kind = entry.Kind.ToString(),
        channel = entry.Channel.ToString(),
        status = entry.Status.ToString(),
        entry.Type,
        entry.Payload,
        entry.CorrelationId,
        entry.IdempotencyKey,
        entry.ReferenceType,
        entry.ReferenceId,
        entry.Attempts,
        entry.MaxAttempts,
        entry.NextAttemptAt,
        entry.LastAttemptAt,
        entry.LastError,
        entry.CreatedAt,
        entry.DispatchedAt
    };

    private static IExternalIntegrationAdapter ResolveAdapter(IntegrationKind kind) => new StubIntegrationAdapter(kind);
    private static IAiAssistProvider ResolveAiProvider() => new StubAiAssistProvider();

    private static async Task<Dictionary<string, bool>> ReadPreferenceMap(OFCDbContext db, Guid branchId, CancellationToken ct)
    {
        var keys = IntegrationRules.DefaultKinds.Select(IntegrationRules.PreferenceKeyFor).ToList();
        var settings = await db.BranchSettings.AsNoTracking().Where(x => x.BranchId == branchId && keys.Contains(x.Key)).ToListAsync(ct);
        return settings.ToDictionary(x => x.Key, x => IntegrationRules.ParseEnabled(x.Value));
    }

    private static async Task<bool> IsEnabled(OFCDbContext db, Guid? branchId, IntegrationKind kind, CancellationToken ct)
    {
        if (branchId is null) return false;
        var setting = await db.BranchSettings.AsNoTracking().Where(x => x.BranchId == branchId && x.Key == IntegrationRules.PreferenceKeyFor(kind)).Select(x => x.Value).FirstOrDefaultAsync(ct);
        return IntegrationRules.ParseEnabled(setting);
    }

    private static bool CanView(ClaimsPrincipal user) => user.HasClaim("permission", "integrations.view") || user.HasClaim("permission", "settings.manage");
    private static bool CanManage(ClaimsPrincipal user) => user.HasClaim("permission", "integrations.manage") || user.HasClaim("permission", "settings.manage");
    private static bool CanAi(ClaimsPrincipal user) => user.HasClaim("permission", "integrations.ai") || user.HasClaim("permission", "settings.manage");

    private static async Task<bool> HasAccess(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static async Task<List<Guid>> AccessibleBranches(OFCDbContext db, ClaimsPrincipal user, Guid? branchId, CancellationToken ct)
    {
        if (branchId.HasValue) return await HasAccess(db, user, branchId.Value, ct) ? [branchId.Value] : [];
        var ids = new HashSet<Guid>();
        if (Guid.TryParse(user.FindFirstValue("branch_id"), out var claim)) ids.Add(claim);
        foreach (var id in await db.UserBranches.AsNoTracking().Where(x => x.UserId == UserId(user)).Select(x => x.BranchId).ToListAsync(ct)) ids.Add(id);
        return ids.ToList();
    }

    private static Guid? NormalizeBranch(ClaimsPrincipal user, Guid? branchId) => branchId.HasValue ? branchId : Guid.TryParse(user.FindFirstValue("branch_id"), out var id) ? id : null;
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record SetPreferenceRequest(Guid BranchId, IntegrationKind Kind, bool Enabled);
    private sealed record EnqueueOutboxRequest(Guid? BranchId, IntegrationKind Kind, IntegrationChannel Channel, string Type, string Payload, string IdempotencyKey, string? CorrelationId = null, string? ReferenceType = null, Guid? ReferenceId = null);
    private sealed record AiSuggestRequest(Guid? BranchId, string? Context);
}

public sealed class StubIntegrationAdapter(IntegrationKind kind) : IExternalIntegrationAdapter
{
    public IntegrationKind Kind => kind;
    public string Name => $"Stub::{kind}";

    public Task<AdapterResult> TestAsync(CancellationToken ct) => Task.FromResult(new AdapterResult(true, "Adapter available in stub mode; no real endpoint is configured."));
    public Task<AdapterResult> DispatchAsync(OutboxMessage message, CancellationToken ct) => Task.FromResult(new AdapterResult(false, "No external provider wired; stub adapter did not deliver (safe no-op)."));
}

public sealed class StubAiAssistProvider : IAiAssistProvider
{
    private static readonly string[] SafeUpsellTitlesAr = ["اعرض وجبة زنجر مع مشروب", "اعرض المضاعفات (فرايز كبير + ر.ع 0.300)", "اعرض صنفاً عالي الهامش مع الطلب"];
    private static readonly string[] SafeUpsellTitlesEn = ["Offer Zinger meal with a drink", "Offer upsell (large fries + OMR 0.300)", "Offer a high-margin item with the order"];

    public Task<AiAssistResult> SuggestAsync(AiAssistRequest request, CancellationToken ct)
    {
        var suggestions = new List<AiSuggestion>
        {
            new(AiSuggestionKind.Upsell, SafeUpsellTitlesAr[0], SafeUpsellTitlesEn[0], "Sample suggestion based on recent branch sales; requires manager approval.", 0.62m),
            new(AiSuggestionKind.Insight, "منتجات الأكثر مبيعاً", "Best-selling products", "Top-selling categories this period.", 0.55m)
        };
        return Task.FromResult(new AiAssistResult(suggestions, "stub", null, SafeFallback: true));
    }
}
