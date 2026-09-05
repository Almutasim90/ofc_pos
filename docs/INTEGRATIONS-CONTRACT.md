# External Integration & AI Extension Contract

Status: Sprint 18 (Good-to-Have). Optional, non-blocking, disabled-by-default.

This document defines the optional extension points added in Sprint 18. None of them are on the core POS / ordering critical path. If every optional service below fails, is disabled, or is simply not configured, the cashier, kitchen, payments, inventory, and reporting flows continue to work unchanged.

## Principles

- **Adapters, not hard coupling.** Core modules never call an external provider directly. They only observe contract abstractions.
- **Disabled by default.** Every integration and the AI assist provider is dormant until an operator enables it. There is no outbound traffic unless a preference is switched on.
- **Fail safely.** Adapter tests and dispatches are wrapped so an exception becomes a recorded `Skipped`/`Failed`/`Deferred` outcome instead of propagating into a request or a core flow.
- **AI is advisory only.** The AI extension point never posts a decision automatically. Every suggestion carries `requiresReview: true` and a confidence value; nothing is applied without a human decision.

## Extension Points

| Kind | Purpose | Interface | Preference key |
| --- | --- | --- | --- |
| `Loyalty` | Customer points / rewards (placeholder) | `IExternalIntegrationAdapter` | `integration.loyalty.enabled` |
| `CustomerCrm` | Customer record enrichment (placeholder) | `IExternalIntegrationAdapter` | `integration.customercrm.enabled` |
| `OnlineOrdering` | Third-party online ordering feed | `IExternalIntegrationAdapter` | `integration.onlineordering.enabled` |
| `Delivery` | Delivery aggregator adapters | `IExternalIntegrationAdapter` | `integration.delivery.enabled` |
| `Notifications` | Webhook / email / SMS notification outbox | `IExternalIntegrationAdapter` + outbox | `integration.notifications.enabled` |
| `AiAssist` | Forecast, upsell & management insight suggestions | `IAiAssistProvider` | `ai.assist.enabled` |
| `ExternalBilling` | SaaS productization / external invoicing | `IExternalIntegrationAdapter` | `integration.externalbilling.enabled` |

Preferences are stored in the existing `branch_settings` table (no dedicated preference table). They are read at request time so the system is always query-time consistent.

## Adapter Contract

```csharp
public interface IExternalIntegrationAdapter
{
    IntegrationKind Kind { get; }
    string Name { get; }
    Task<AdapterResult> TestAsync(CancellationToken ct);
    Task<AdapterResult> DispatchAsync(OutboxMessage message, CancellationToken ct);
}
```

Sprint 18 ships a safe `StubIntegrationAdapter` that returns a benign `AdapterResult` and never contacts the network. A real provider implements this interface and is registered for its `IntegrationKind` in `SprintEighteenEndpoints`.

## AI Assist Contract

```csharp
public interface IAiAssistProvider
{
    Task<AiAssistResult> SuggestAsync(AiAssistRequest request, CancellationToken ct);
}
```

Rules enforced by `IntegrationRules`:

- `SuggestionAcceptsWithoutReview(...)` always returns `false` (no auto-apply).
- `IsSafeAiResult(...)` is true only when the result is a degraded safe fallback or every suggestion requires review.
- The endpoint wraps provider calls in `try/catch`. On any failure it returns `mode = "degraded"` with an empty suggestion list and `safeFallback = true`, still HTTP 200, and an audit entry. Core POS/ordering is never touched.

## Notification/Webhook Outbox Foundation

The `external_outbox` table is the durable, idempotent foundation for outbound notifications and webhooks. It stores the intent to deliver and retries until success or a maximum attempt count.

| Column | Meaning |
| --- | --- |
| `Id` | UUIDv7 identifier. |
| `BranchId` | Owning branch (nullable; `SetNull` on branch delete). |
| `Kind` / `Channel` | Integration kind and delivery channel (Webhook / Email / Sms). |
| `Type` | Event type, e.g. `order.completed`. |
| `Payload` | `jsonb` event payload. |
| `Status` | `Queued` / `Dispatching` / `Dispatched` / `Failed` / `Skipped` / `Deferred`. |
| `CorrelationId` | Trace correlation for opentelemetry/audit cross-link. |
| `IdempotencyKey` | Unique per `(BranchId, IdempotencyKey)`; a repeated key returns the prior record. |
| `ReferenceType` / `ReferenceId` | Optional link to a source entity (order, customer, …). |
| `Attempts` / `MaxAttempts` | Delivery attempt counters (default max = 5). |
| `NextAttemptAt` | Backoff gate (exponential: 30s · 2^(attempt−1)). |
| `LastAttemptAt` / `LastError` / `DispatchedAt` | Dispatch audit. |

Status transitions (from `IntegrationRules`):

```text
Queued → (disabled) Skipped
Queued → Dispatching → Dispatched      (success)
Queued → Dispatching → Deferred/Failed (failure, retried after NextAttemptAt)
```

A disabled integration is recorded as `Skipped` ("Disabled by preference") and is never delivered. When no real adapter is registered for an enabled kind, dispatch succeeds-to-stub as `Deferred`/`Failed` with a "not configured" reason, keeping the retry loop honest.

## API

All endpoints are authorized and scoped to branches the user can access. Sensitive operations (preference changes, outbox enqueue/dispatch, AI requests) are written to the audit log. Permission fallback: a user with `settings.manage` may use every Sprint 18 endpoint; granular codes `integrations.view`, `integrations.manage`, and `integrations.ai` refine access.

| Method & path | Purpose |
| --- | --- |
| `GET /api/v1/integrations/preferences?branchId=` | List extension points and enabled state. |
| `POST /api/v1/integrations/preferences` | Enable/disable an extension point (body: `branchId`, `kind`, `enabled`). |
| `POST /api/v1/integrations/preferences/{kind}/test?branchId=` | Safe adapter connectivity test. |
| `POST /api/v1/integrations/outbox` | Enqueue a notification/webhook outbox record. |
| `GET /api/v1/integrations/outbox?branchId=&status=` | List recent outbox records (max 100). |
| `POST /api/v1/integrations/outbox/{id}/dispatch` | Attempt one safe dispatch. |
| `POST /api/v1/integrations/ai/suggest` | Return advisory AI suggestions (never applied automatically). |

## Adding a Real Provider

1. Implement `IExternalIntegrationAdapter` and/or `IAiAssistProvider` in the module or a dedicated provider assembly.
2. Register it in `SprintEighteenEndpoints.ResolveAdapter` / `ResolveAiProvider` (or inject via DI).
3. Keep the provider as a separate dependency so no external SDK leaks into core modules.
4. Ensure the provider is naturally idempotent on `IdempotencyKey` and never throws into a core request.

Secrets are configured outside tracked configuration, per the SRS secret rules. The outbox payload must not carry API keys or tokens.
