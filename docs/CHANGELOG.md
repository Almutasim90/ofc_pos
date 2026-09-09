# Changelog

All notable project changes are recorded in this file.

## Unreleased

### Realtime QR-order notifications (2026-09-09)

- Added an authorized `OrdersHub` at `/hubs/orders` with an `IOrdersBroadcaster`; a freshly submitted QR order and every QR approval review now broadcast `qrOrderReceived`/`qrOrderReviewed` to the branch group. SignalR stays transport-only — screens re-fetch authoritative REST data on each event.
- The QR customer page now raises a notification toast on submit and on tracked status changes, and shows a live inline status message (e.g. "Your order has been sent and approved", awaiting staff approval, sent to the kitchen, preparing/ready/completed, cancelled/rejected). It is driven by the existing status poll, so nothing requires a page refresh.
- The QR admin screen and the POS/cashier screen subscribe to the hub: a new QR order or a review outcome refreshes their order/held lists in the background and shows a notification toast (plus a "Live" pill on the QR admin pending list) — a true SPA update without reloading.

### Usability and daily workflows (2026-09-08)

- POS-first navigation with grouped menus, breadcrumbs and hash routes; responsive product browsing, cart, direct kitchen dispatch and current-order payment follow-up.
- Searchable product/category management with edit/deactivate, photo previews, optional advanced details, automatic internal product barcodes and barcode/SKU search.
- Editable user profiles and branch assignments with session revocation; editable inventory items with protected base units and historical costs.
- Locally generated downloadable QR images linked to the public ordering page; clarified kitchen routing and physical count instructions, including required counted quantities.
- Inventory task tabs, API/WebSocket development proxy, and kitchen WebSocket/static-image routing for Nginx.
- See `USABILITY-REVIEW-2026-09-08.md` for findings, workflow instructions and verification scope.

### Payment lifecycle follow-up (2026-09-08)

- Completed the pending electronic authorization/capture and payment reversal endpoints. Authorization does not post a sale; the last capture marks the order Paid, including split tenders.
- Fixed newly appended order/payment history entries being tracked as updates, which caused HTTP 500 during order status changes and payment settlement.
- Reversals require a reason and an original sale, append a linked reversal, and reopen a Paid order as Pending. Replacement tenders cover only the outstanding amount. Orders beyond payment use the existing refund workflow.
- Added integration coverage for ledger entries, sequential duplicate requests, split capture and replacement, cancelled-order capture rejection, and reversal validation.
- Validation uses EF InMemory for HTTP integration tests; PostgreSQL constraints, concurrent payment requests, and actual terminal/provider callbacks still require a separate production-like verification. No deployment or database migration was performed in this follow-up.

### Added

- STORY-18-01/02/09 Optional extension points & preferences: `OFC.Modules.Integrations` (adapter contracts `IExternalIntegrationAdapter`/`IAiAssistProvider`, `IntegrationRules`) with loyalty, customer CRM, online ordering, delivery, notifications, AI assist, and external-billing placeholder adapters. Preferences are read from the existing `branch_settings` table at query time (keys `integration.<kind>.enabled`, `ai.assist.enabled`), all disabled by default.
- STORY-18-05 Notification/webhook outbox foundation: durable `ExternalOutboxEntry` (`external_outbox`) with idempotency key, statuses (Queued/Dispatching/Dispatched/Failed/Skipped/Deferred), exponential retry/backoff, and a unique `(BranchId, IdempotencyKey)` index. Safe dispatch records a benign outcome instead of throwing.
- STORY-18-06/07/08 Non-blocking AI assist: `POST /api/v1/integrations/ai/suggest` returns advisory suggestions flagged `requiresReview: true`; `IntegrationRules.SuggestionAcceptsWithoutReview` always returns `false`, and the endpoint degrades to `mode = "degraded"` with `safeFallback = true` on any failure, never posting or mutating an order.
- New `/api/v1/integrations` endpoints (`preferences`, `preferences/{kind}/test`, `outbox`, `outbox/{id}/dispatch`, `ai/suggest`) with server-side authorization (`integrations.view`/`integrations.manage`/`integrations.ai` with `settings.manage` fallback), validation, and audit of sensitive operations.
- `ExternalIntegrationContract` documentation (`docs/INTEGRATIONS-CONTRACT.md`) and bilingual responsive `IntegrationsSection` UI (extension point toggles/test, outbox foundation, AI assist panel).
- `IntegrationRulesTests` covering preference keys, enabled parsing, outbox validation, retry/backoff and attempt gating, AI safety, and sensitive/review classification; migration `20260905104237_AddSprintEighteenIntegrationsAi`.


- STORY-15-01 Physical inventory counts: `InventoryCount`/`InventoryCountLine` sessions capture system quantity, counted quantity, and variance.
- STORY-15-02 Count variance: posting an approved count emits `CountAdjustment` ledger movements; variance lines require a reason and a dedicated approval step.
- STORY-15-03 Stock transfer: `StockTransfer`/`StockTransferLine` with ship/receive flow; the destination branch does not increase until receipt (`TransferOut` on ship, `TransferIn` on receive).
- STORY-15-04 Waste categories: auditable `WasteRecord` (Expired, Damaged, PreparationWaste, FinishedProductWaste, CancelledOrderWaste) linked to a `Waste` ledger movement, with reason, optional note and photo.
- STORY-15-05 Cancelled-order waste: record waste against an order/order line (`OrderId`, `OrderLineId`) and the cancelled-order category.
- STORY-15-06 Weighted average cost: inventory valuation uses current weighted-average unit cost; goods-receipt posting already blends stock (procurement costing) and is exposed as branch valuation.
- STORY-15-07 Recipe cost: reproducible recipe cost via `InventoryRules.TryComputeRecipeCost` over ingredient quantities, conversions, and weighted-average unit costs.
- STORY-15-08 Food cost %: `InventoryRules.FoodCostPercent` (recipe cost / selling price).
- STORY-15-09 Gross margin: `InventoryRules.GrossMargin` and `GrossMarginPercent`.
- New endpoints under `/api/v1/inventory`: `/counts*`, `/transfers*`, `/waste*`, `/costing*` with server-side authorization, validation, audit, and idempotency keys (`ClientCountId`, `ClientTransferId`, `ClientRecordId`).
- `OFC.Modules.Inventory` advanced entities/rules, advanced inventory permissions in `IdentityService`, migration `20260905093141_AddSprintFifteenAdvancedInventoryWasteCosting`, and bilingual responsive `AdvancedInventorySection` UI.
- `InventoryAdvancedRulesTests` covering count variance, count/transfer state machines, waste quantity, reproducible recipe cost, food cost %, and gross margin.

- STORY-13-01 Unified audit log query: `GET /api/v1/audit-logs` with filters (branch, user, action, entity type/id, range) and pagination, plus retention principles (`ReportingRules`).
- STORY-13-02 Sales reports: `GET /api/v1/reports/sales` with daily, by-branch, by-product, by-category, by-channel, by-cashier, by-payment-method breakdowns plus discount and tax totals.
- STORY-13-03 Payment reports: `GET /api/v1/reports/payments` by method/status and refund totals.
- STORY-13-04 Refund/void/cancel reports: `GET /api/v1/reports/cancellations` with top reasons, by-user/channel/hour, before/after kitchen, void and refund totals.
- STORY-13-05 Shift variance: `GET /api/v1/reports/shifts/cash` with per-shift expected/actual cash and variance.
- STORY-13-06 Low stock: `GET /api/v1/reports/inventory/low-stock` and movement summary `GET /api/v1/reports/inventory`.
- STORY-13-07 Basic KDS metrics: `GET /api/v1/reports/kitchen` (throughput, avg prep, late orders, per station/channel).
- STORY-13-08 Dashboard: `GET /api/v1/reports/dashboard` KPI set; plus `GET /api/v1/reports/channels` channel/price breakdown.
- Export foundation: `GET /api/v1/reports/export` producing auditable CSV (`report_exports`) with authorization/audit.
- `OFC.Modules.Reporting` module, dashboard/bilingual responsive reports UI, and migration `20260905084845_AddSprintThirteenAuditCoreReports`.

- STORY-00-01 repository layout for frontend, backend, tests, infrastructure, and documentation.
- Canonical project handoff documentation.
- STORY-00-02 ASP.NET Core modular-monolith solution targeting .NET 10 LTS.
- API health endpoint at `GET /health`.
- Backend assembly-boundary documentation.
- STORY-00-03 Vite React TypeScript application with Tailwind 4 integration.
- shadcn/ui configuration and utility prerequisites.
- Frontend foundation documentation.
- STORY-00-04 PostgreSQL EF Core foundation with the Npgsql provider and `OFCDbContext`.
- Initial `ofc` application-schema migration and migration documentation.
- STORY-12-01 Local transaction queue: a browser outbox (`apps/web/src/lib/sync-outbox.ts`) persists pending offline operations.
- STORY-12-02 Stable idempotency keys: `SyncOperation.IdempotencyKey` with a unique `(BranchId, DeviceId, IdempotencyKey)` index; a repeated key returns the prior result without a second transaction.
- STORY-12-03 Retry/backoff: the recovery UI retries stalled syncs with exponential backoff and re-flushes on reconnect.
- STORY-12-04 Conflict states: `SyncOperationStatus.Conflict` and `ConflictReason` surface stale-pricing and entity-conflict conditions for operator action.
- STORY-12-05 Server acknowledgement: `POST /api/v1/sync` accepts a batch and returns a per-operation result (applied/duplicate/conflict/failed) with the server version.
- STORY-12-06 Recovery UI: bilingual, responsive `SyncSection` showing connectivity, pending operations, conflicts, and last-synced state.
- STORY-12-07 Stale pricing warnings: offline orders are applied from their frozen price snapshots; a catalog-version mismatch flags `stale-pricing` instead of repricing.
- STORY-12-08 Negative-stock discrepancy handling: an offline movement that drives stock negative is flagged `negative-stock` for review.
- `OFC.Modules.Sync` module, `POST /api/v1/sync`, `GET /api/v1/sync/state`, server-side authorization and audit, and migration `20260905082548_AddSprintTwelveOfflineSync`.

### Changed

- Relocated the Agile Delivery Pack to `docs/` to match the documented project convention.
