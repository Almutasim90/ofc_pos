# Changelog

All notable project changes are recorded in this file.

## Unreleased

### Added

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
