# Project State

## Current Sprint

Sprint 18 - Good-to-Have Integrations & AI (implemented, awaiting review gate).

## Completed Stories

- STORY-00-01 through STORY-00-10: Engineering foundation, identity/org/branches/devices.
- STORY-01-01 through STORY-11-xx: Catalog, combos/modifiers, pricing/tax, POS ordering, payments, cancellation/void/refund, shifts/cash, printing/hardware, KDS/kitchen continuity, inventory/UOM/BOM.
- STORY-12-01 through STORY-12-08: Offline sync & recovery (local queue, idempotency, retry/backoff, conflict states, server acknowledgement, recovery UI, stale pricing, negative stock).
- STORY-13-01 through STORY-13-08: Audit & core reports (audit log, sales, payments, cancellations, shift variance, low stock/inventory, KDS metrics, dashboard), export foundation and Reports & Audit UI.
- Sprint 14: Procurement & Suppliers (suppliers, purchase orders workflow, goods receipts, supplier history, procurement costing via weighted average cost).
- STORY-15-01 through STORY-15-09: Physical counts, count variance, stock transfer, waste categories, cancelled-order waste, weighted average cost, recipe cost, food cost %, gross margin.
- STORY-16-01 through STORY-16-07: QR context + branch/table/parking, customer menu, modifiers/combos, anonymous order submission, staff approval mode, live tracking, customer identity/flow (walk-in + optional customer record link). Unified via `OrderingEngine` shared by POS and QR.
- Sprint 17: Advanced reporting & management (branch comparison, cancellation analytics, kitchen performance, food cost dashboard, inventory trends, profit & loss, operational alerts, combined with export).
- STORY-18-01 through STORY-18-09: Good-to-have integrations & AI (optional extension points & preferences, loyalty/customer CRM/online ordering/delivery/notifications/billing adapters, webhook & notification outbox foundation, non-blocking AI assist extension point, SaaS readiness contract).

## Current Story

Sprint 18 review gate.

## Implemented Modules

- `OFC.Api`: single ASP.NET Core API host with health, admin, catalog, POS, QR ordering, inventory, sync, audit, reports, and integrations endpoints.
- `OFC.Infrastructure`: technical composition assembly (db, identity, persistence).
- `OFC.SharedKernel`: business-neutral shared assembly (offline ids).
- `OFC.Modules.Sync`: offline sync entities and rules (`SyncOperation`, `SyncState`, `SyncRules`).
- `OFC.Modules.Reporting`: report export entity (auditable CSV) and reporting/retention rules (`ReportExport`, `ReportingRules`).
- `OFC.Modules.Inventory` advanced: inventory count sessions, stock transfers, waste records, and costing rules (variance, state machines, reproducible recipe cost, food cost %, gross margin).
- `OFC.Modules.QrOrdering`: QR context (`QrContext`), order approval (`QrOrderApproval`), and customer identity (`Customer`) with QR rules (`QrRules`).
- `OFC.Modules.Integrations`: integration/notification/AI extension contracts, adapter + AI-assist rules, and the notification/webhook outbox entity (`ExternalOutboxEntry`, `IntegrationRules`, `IExternalIntegrationAdapter`, `IAiAssistProvider`).
- Business modules: Identity, Organization, Catalog, Ordering, Payments, Shifts, Printing, Kitchen, Inventory, Reporting, Sync, Procurement.
- `OrderingEngine` (in `OFC.Modules.Ordering`): the shared pricing/selection/order-line builder now used by both the POS order endpoint and the QR order endpoint (same `PricingRules` + `CatalogRules` engine, one unified path).
- `apps/web`: Vite React TypeScript application with a bilingual, responsive shell, POS, inventory, sync recovery, Procurement, a Reports & Audit section, an Advanced Inventory section, a QR admin section, and an Integrations & AI section; a public hash-routed customer QR ordering page (`#/qr/<code>`).
- Sprint 16 endpoints: customer-facing `/api/v1/qr/{code}` context, `/{code}/menu` menu, `/{code}/orders` submit, `/{code}/orders/{clientRequestId}` track (all anonymous), plus authorized `/api/v1/qr/contexts*`, `/api/v1/qr/orders`, and `/api/v1/qr/approvals/{id}/review`. QR orders use `OrderSource.Qr` and flow through the existing order/payment/kitchen pipeline.
- Sprint 18 endpoints: authorized `/api/v1/integrations/preferences` (list + set), `/preferences/{kind}/test`, `/outbox` (enqueue + list + dispatch), and `/ai/suggest`, all scoped to accessible branches, audit-logged, and disabled-by-default so core POS/ordering is unaffected.

## Latest Migration

`20260905104237_AddSprintEighteenIntegrationsAi` (migration file generated; not applied to the database — no working `ConnectionStrings__DefaultConnection` was configured in this environment). Creates the `external_outbox` table in `ofc` with a unique `(BranchId, IdempotencyKey)` index and a `(BranchId, Status, CreatedAt)` index. Sprint 18 preferences need no migration: they reuse the existing `branch_settings` table.

## Architecture Decisions

- The OFC project is an isolated Git repository rooted at this directory.
- Product and delivery documentation is canonical under `docs/`.
- Application code is reserved for `apps/web/` and `backend/src/`; tests belong in `backend/tests/`.
- The backend targets .NET 10 LTS and uses a modular-monolith dependency direction.
- The frontend uses Vite, React, TypeScript, Tailwind 4, and shadcn/ui conventions.
- OFC-owned database objects use the `ofc` schema; the EF migration-history table remains in PostgreSQL's default schema.
- Offline operations carry a stable idempotency key; the server never re-prices an offline sale.
- QR ordering is customer-created; the Order creator and status-history actor are nullable so customer orders are not attributed to a staff user. A QR context always records an approval row (`QrOrderApproval`), even when auto-approved, to link order ↔ QR context ↔ customer and to audit the decision.
- Sprint 18 extension contracts (integrations/notifications/AI) are hosted in one `OFC.Modules.Integrations` assembly; they are disabled by default, read from `branch_settings` at query time, and never sit on the core POS/ordering critical path. Only `external_outbox` is a new persisted table (the durable outbound notification/webhook foundation). See `DECISIONS.md` ADR-0007/ADR-0008.

## Known Issues

- Database credentials disclosed in chat must be rotated. Local development accesses PostgreSQL through an SSH tunnel.

## Next Recommended Story

None reserved; Sprint 18 completes the planned good-to-have backdrop. Remaining work is any follow-up provider wiring or a dedicated Sprint 19, to be prioritized against the backlog.

## Do-Not-Change Rules

- Keep PostgreSQL as the final source of truth and IndexedDB/localStorage as an operational local store.
- Preserve Arabic RTL and English LTR as cross-cutting requirements from the first UI implementation.
- Do not add business logic to React components or API controllers.
- The Sprint 12 migration must be applied deliberately and is not part of an automated test run.
- Sprint 18 extension points must remain on the good-to-have path: disabled by default and never blocking core POS, payment, kitchen, printing, inventory, or ordering.
