# Sprint 08 — Shifts & Cash Control

## Sprint Goal
تطبيق فتح الوردية وحركات النقد والتقفيل الأعمى.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-08-01: Open shift
- STORY-08-02: Opening float
- STORY-08-03: Cash movements
- STORY-08-04: Petty cash
- STORY-08-05: Cash denominations
- STORY-08-06: Blind close
- STORY-08-07: Cash/card variance
- STORY-08-08: Supervisor review

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
- Cashier cannot see expected cash before close
- Variance is calculated correctly
- Cash denomination sum matches entered cash
- Shift close is audited

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
