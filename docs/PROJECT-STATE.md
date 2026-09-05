# Project State

## Current Sprint

Sprint 15 - Advanced Inventory, Waste & Costing (implemented, awaiting review gate).

## Completed Stories

- STORY-00-01 through STORY-00-10: Engineering foundation, identity/org/branches/devices.
- STORY-01-01 through STORY-11-xx: Catalog, combos/modifiers, pricing/tax, POS ordering, payments, cancellation/void/refund, shifts/cash, printing/hardware, KDS/kitchen continuity, inventory/UOM/BOM.
- STORY-12-01 through STORY-12-08: Offline sync & recovery (local queue, idempotency, retry/backoff, conflict states, server acknowledgement, recovery UI, stale pricing, negative stock).
- STORY-13-01 through STORY-13-08: Audit & core reports (audit log, sales, payments, cancellations, shift variance, low stock/inventory, KDS metrics, dashboard), export foundation and Reports & Audit UI.
- Sprint 14: Procurement & Suppliers (suppliers, purchase orders workflow, goods receipts, supplier history, procurement costing via weighted average cost).
- STORY-15-01 through STORY-15-09: Physical counts, count variance, stock transfer, waste categories, cancelled-order waste, weighted average cost, recipe cost, food cost %, gross margin.

## Current Story

Sprint 15 review gate.

## Implemented Modules

- `OFC.Api`: single ASP.NET Core API host with health, admin, catalog, POS, inventory, sync, audit, and reports endpoints.
- `OFC.Infrastructure`: technical composition assembly (db, identity, persistence).
- `OFC.SharedKernel`: business-neutral shared assembly (offline ids).
- `OFC.Modules.Sync`: offline sync entities and rules (`SyncOperation`, `SyncState`, `SyncRules`).
- `OFC.Modules.Reporting`: report export entity (auditable CSV) and reporting/retention rules (`ReportExport`, `ReportingRules`).
- `OFC.Modules.Inventory` advanced: inventory count sessions, stock transfers, waste records, and costing rules (variance, state machines, reproducible recipe cost, food cost %, gross margin).
- Business modules: Identity, Organization, Catalog, Ordering, Payments, Shifts, Printing, Kitchen, Inventory, Reporting, Sync, Procurement.
- `apps/web`: Vite React TypeScript application with a bilingual, responsive shell, POS, inventory, sync recovery, Procurement, and a Reports & Audit section, plus an Advanced Inventory (counts, waste, costing) section.
- Offline sync foundation: browser outbox, idempotent batch sync endpoint (`POST /api/v1/sync`), sync state (`GET /api/v1/sync/state`), retry/backoff, and recover-on-reconnect.
- Core reports foundation: audit log (`GET /api/v1/audit-logs`), sales, payments, cancellations/voids/refunds, shift cash variance, inventory movement/low stock, kitchen metrics, channel/price breakdown, dashboard, and CSV export.
- Sprint 15 endpoints under `/api/v1/inventory`: `/counts*` (create/approve/post/cancel), `/transfers*` (create/ship/receive/cancel), `/waste*` (record/list/categories), `/costing*` (valuation, recipe cost, food-cost/gross-margin summary), all server-authorized, audited, and offline idempotent.

## Latest Migration

`20260905093141_AddSprintFifteenAdvancedInventoryWasteCosting` (migration file generated; not applied to the database — no working `ConnectionStrings__DefaultConnection` was configured in this environment).

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

Begin Sprint 16: QR Ordering & Customer Flow.

## Do-Not-Change Rules

- Keep PostgreSQL as the final source of truth and IndexedDB/localStorage as an operational local store.
- Preserve Arabic RTL and English LTR as cross-cutting requirements from the first UI implementation.
- Do not add business logic to React components or API controllers.
- The Sprint 12 migration must be applied deliberately and is not part of an automated test run.
