# Project State

## Current Sprint

Sprint 13 - Audit & Core Reports (implemented, awaiting review gate).

## Completed Stories

- STORY-00-01 through STORY-00-10: Engineering foundation, identity/org/branches/devices.
- STORY-01-01 through STORY-11-xx: Catalog, combos/modifiers, pricing/tax, POS ordering, payments, cancellation/void/refund, shifts/cash, printing/hardware, KDS/kitchen continuity, inventory/UOM/BOM.
- STORY-12-01 through STORY-12-08: Offline sync & recovery (local queue, idempotency, retry/backoff, conflict states, server acknowledgement, recovery UI, stale pricing, negative stock).
- STORY-13-01: Unified audit log query/api with retention principles.
- STORY-13-02: Sales reports.
- STORY-13-03: Payment reports.
- STORY-13-04: Refund/void/cancel reports.
- STORY-13-05: Shift variance.
- STORY-13-06: Low stock and inventory movement.
- STORY-13-07: Basic KDS metrics.
- STORY-13-08: Dashboard and channel/price breakdown.
- Sprint 13 export foundation (auditable CSV) and bilingual responsive Reports & Audit UI.

## Current Story

Sprint 13 review gate.

## Implemented Modules

- `OFC.Api`: single ASP.NET Core API host with health, admin, catalog, POS, inventory, sync, audit, and reports endpoints.
- `OFC.Infrastructure`: technical composition assembly (db, identity, persistence).
- `OFC.SharedKernel`: business-neutral shared assembly (offline ids).
- `OFC.Modules.Sync`: offline sync entities and rules (`SyncOperation`, `SyncState`, `SyncRules`).
- `OFC.Modules.Reporting`: report export entity (auditable CSV) and reporting/retention rules (`ReportExport`, `ReportingRules`).
- Business modules: Identity, Organization, Catalog, Ordering, Payments, Shifts, Printing, Kitchen, Inventory.
- `apps/web`: Vite React TypeScript application with a bilingual, responsive shell, POS, inventory, sync recovery, and a Reports & Audit section.
- Offline sync foundation: browser outbox, idempotent batch sync endpoint (`POST /api/v1/sync`), sync state (`GET /api/v1/sync/state`), retry/backoff, and recover-on-reconnect.
- Core reports foundation: audit log (`GET /api/v1/audit-logs`), sales, payments, cancellations/voids/refunds, shift cash variance, inventory movement/low stock, kitchen metrics, channel/price breakdown, dashboard, and CSV export.

## Latest Migration

`20260905084845_AddSprintThirteenAuditCoreReports` (applied to the development database; all Sprint 00-13 migrations applied).

## Architecture Decisions

- The OFC project is an isolated Git repository rooted at this directory.
- Product and delivery documentation is canonical under `docs/`.
- Application code is reserved for `apps/web/` and `backend/src/`; tests belong in `backend/tests/`.
- The backend targets .NET 10 LTS and uses a modular-monolith dependency direction.
- The frontend uses Vite, React, TypeScript, Tailwind 4, and shadcn/ui conventions.
- OFC-owned database objects use the `ofc` schema; the EF migration-history table remains in PostgreSQL's default schema.
- Offline operations carry a stable idempotency key; the server never re-prices an offline sale.

## Known Issues

- Database credentials disclosed in chat must be rotated. Local development accesses PostgreSQL through an SSH tunnel.

## Next Recommended Story

Begin Sprint 14: Procurement & Suppliers.

## Do-Not-Change Rules

- Keep PostgreSQL as the final source of truth and IndexedDB/localStorage as an operational local store.
- Preserve Arabic RTL and English LTR as cross-cutting requirements from the first UI implementation.
- Do not add business logic to React components or API controllers.
- The Sprint 12 migration must be applied deliberately and is not part of an automated test run.
