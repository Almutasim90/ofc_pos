# Sprint 13 — Audit & Core Reports

## Sprint Goal
إكمال سجل التدقيق والتقارير التشغيلية الأساسية.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-13-01: Audit log
- STORY-13-02: Sales reports
- STORY-13-03: Payment reports
- STORY-13-04: Refund/void/cancel reports
- STORY-13-05: Shift variance
- STORY-13-06: Low stock
- STORY-13-07: Basic KDS metrics
- STORY-13-08: Dashboard

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
- Owner can trace sensitive operation to user/device/time
- Reports reconcile with ledger
- Cancellation reasons are measurable
- Role-based report access works

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
