# Sprint 14 — Procurement & Suppliers

## Sprint Goal
ربط دخول المخزون بالموردين والاستلام.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-14-01: Suppliers
- STORY-14-02: Purchase entry
- STORY-14-03: Purchase orders
- STORY-14-04: Goods receipt
- STORY-14-05: Supplier cost history
- STORY-14-06: Warehouse receipt

## Cross-Cutting Requirements
- Responsive UI
- Arabic RTL + English LTR
- Server-side authorization
- Validation
- Structured logging
- Audit where sensitive
- Offline-aware design where relevant
- Automated tests

## Acceptance Criteria
- Goods receipt creates inventory movement
- Supplier purchase history is traceable
- Received qty/cost validates correctly

## Definition of Done
Refer to `04-GLOBAL-DEFINITION-OF-DONE.md`.

## Sprint Review Gate
1. Automated tests pass.
2. Acceptance criteria demonstrated.
3. Architecture review completed.
4. UX review completed.
5. No critical technical debt is deferred silently.
6. Sprint approved before starting dependent work.

## Out of Scope
Do not implement later-sprint features unless required strictly as a technical foundation.
