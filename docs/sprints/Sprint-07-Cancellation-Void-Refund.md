# Sprint 07 — Cancellation, Void & Refund

## Sprint Goal
بناء رقابة الإلغاء والاسترجاع مع الأسباب والاعتمادات.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-07-01: Cancellation reasons
- STORY-07-02: Mandatory reason
- STORY-07-03: Void item
- STORY-07-04: Cancel order
- STORY-07-05: Refund
- STORY-07-06: Partial refund
- STORY-07-07: Approval thresholds
- STORY-07-08: Cancellation audit
- STORY-07-09: Cancellation reports

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
- Order is never hard deleted
- Other reason requires note
- Owner can report reason/user/branch/value/time
- Refund and inventory return are separate decisions

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
