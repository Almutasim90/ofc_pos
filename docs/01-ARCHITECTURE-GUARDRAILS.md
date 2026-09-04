# OFC Architecture Guardrails

## Core Architecture
- Modular Monolith.
- Modules: Identity, Organization, Catalog, Pricing, Ordering, Payments, Kitchen, Shifts, Inventory, Procurement, Expenses, Reporting, Notifications, Integrations, AI.
- PostgreSQL = Source of Truth.
- IndexedDB/Dexie = Local Operational Store.
- SignalR = realtime UI transport only, not source of truth.
- Local Print Agent isolates browser from hardware.

## Cross-Cutting Rules
- Arabic RTL and English LTR from Sprint 0.
- Responsive design from Sprint 0.
- Audit hooks from Sprint 0.
- Offline-compatible identifiers and timestamps from Sprint 0.
- UUIDv7/UUID for business transactions.
- Decimal for money.
- TIMESTAMPTZ for server timestamps.
- Server-side authorization.
- Structured logging + correlation IDs.
- No business logic in React components or API controllers.

## Offline Rules
- Price Snapshot is immutable for an order line once created.
- Backend must not re-price an offline-completed transaction during sync.
- Every sync transaction has stable IdempotencyKey.
- Duplicate requests return prior result without duplicate posting.
- KDS failure triggers controlled print fallback.

## UX Rules
- Workflows, not CRUD screens.
- Frequent actions visible; rare actions contextual.
- Touch friendly.
- POS primary flow should remain on one screen.
- Mobile layouts are redesigned, not shrunk desktop screens.
