# Project State

## Current Sprint

Sprint 16 - QR Ordering & Customer Flow (implemented, awaiting review gate).

## Completed Stories

- STORY-00-01 through STORY-00-10: Engineering foundation, identity/org/branches/devices.
- STORY-01-01 through STORY-11-xx: Catalog, combos/modifiers, pricing/tax, POS ordering, payments, cancellation/void/refund, shifts/cash, printing/hardware, KDS/kitchen continuity, inventory/UOM/BOM.
- STORY-12-01 through STORY-12-08: Offline sync & recovery (local queue, idempotency, retry/backoff, conflict states, server acknowledgement, recovery UI, stale pricing, negative stock).
- STORY-13-01 through STORY-13-08: Audit & core reports (audit log, sales, payments, cancellations, shift variance, low stock/inventory, KDS metrics, dashboard), export foundation and Reports & Audit UI.
- Sprint 14: Procurement & Suppliers (suppliers, purchase orders workflow, goods receipts, supplier history, procurement costing via weighted average cost).
- STORY-15-01 through STORY-15-09: Physical counts, count variance, stock transfer, waste categories, cancelled-order waste, weighted average cost, recipe cost, food cost %, gross margin.
- STORY-16-01 through STORY-16-07: QR context + branch/table/parking, customer menu, modifiers/combos, anonymous order submission, staff approval mode, live tracking, customer identity/flow (walk-in + optional customer record link). Unified via `OrderingEngine` shared by POS and QR.

## Current Story

Sprint 16 review gate.

## Implemented Modules

- `OFC.Api`: single ASP.NET Core API host with health, admin, catalog, POS, QR ordering, inventory, sync, audit, and reports endpoints.
- `OFC.Infrastructure`: technical composition assembly (db, identity, persistence).
- `OFC.SharedKernel`: business-neutral shared assembly (offline ids).
- `OFC.Modules.Sync`: offline sync entities and rules (`SyncOperation`, `SyncState`, `SyncRules`).
- `OFC.Modules.Reporting`: report export entity (auditable CSV) and reporting/retention rules (`ReportExport`, `ReportingRules`).
- `OFC.Modules.Inventory` advanced: inventory count sessions, stock transfers, waste records, and costing rules (variance, state machines, reproducible recipe cost, food cost %, gross margin).
- `OFC.Modules.QrOrdering`: QR context (`QrContext`), order approval (`QrOrderApproval`), and customer identity (`Customer`) with QR rules (`QrRules`).
- Business modules: Identity, Organization, Catalog, Ordering, Payments, Shifts, Printing, Kitchen, Inventory, Reporting, Sync, Procurement.
- `OrderingEngine` (in `OFC.Modules.Ordering`): the shared pricing/selection/order-line builder now used by both the POS order endpoint and the QR order endpoint (same `PricingRules` + `CatalogRules` engine, one unified path).
- `apps/web`: Vite React TypeScript application with a bilingual, responsive shell, POS, inventory, sync recovery, Procurement, a Reports & Audit section, plus an Advanced Inventory section and a QR admin section; a public hash-routed customer QR ordering page (`#/qr/<code>`).
- Sprint 16 endpoints: customer-facing `/api/v1/qr/{code}` context, `/{code}/menu` menu, `/{code}/orders` submit, `/{code}/orders/{clientRequestId}` track (all anonymous), plus authorized `/api/v1/qr/contexts*`, `/api/v1/qr/orders`, and `/api/v1/qr/approvals/{id}/review`. QR orders use `OrderSource.Qr` and flow through the existing order/payment/kitchen pipeline.

## Latest Migration

`20260905100045_AddSprintSixteenQrOrderingCustomerFlow` (migration file generated; not applied to the database — no working `ConnectionStrings__DefaultConnection` was configured in this environment). Creates `customers`, `qr_contexts`, `qr_order_approvals`, adds `orders.CustomerId`, and makes `orders.CreatedByUserId` / `order_status_history.ChangedByUserId` nullable for customer-created orders.

## Architecture Decisions

- The OFC project is an isolated Git repository rooted at this directory.
- Product and delivery documentation is canonical under `docs/`.
- Application code is reserved for `apps/web/` and `backend/src/`; tests belong in `backend/tests/`.
- The backend targets .NET 10 LTS and uses a modular-monolith dependency direction.
- The frontend uses Vite, React, TypeScript, Tailwind 4, and shadcn/ui conventions.
- OFC-owned database objects use the `ofc` schema; the EF migration-history table remains in PostgreSQL's default schema.
- Offline operations carry a stable idempotency key; the server never re-prices an offline sale.
- QR ordering is customer-created; the Order creator and status-history actor are nullable so customer orders are not attributed to a staff user. A QR context always records an approval row (`QrOrderApproval`), even when auto-approved, to link order ↔ QR context ↔ customer and to audit the decision.

## Known Issues

- Database credentials disclosed in chat must be rotated. Local development accesses PostgreSQL through an SSH tunnel.

## Next Recommended Story

Sprint 17: Advanced Reporting & Management.

## Do-Not-Change Rules

- Keep PostgreSQL as the final source of truth and IndexedDB/localStorage as an operational local store.
- Preserve Arabic RTL and English LTR as cross-cutting requirements from the first UI implementation.
- Do not add business logic to React components or API controllers.
- The Sprint 12 migration must be applied deliberately and is not part of an automated test run.
