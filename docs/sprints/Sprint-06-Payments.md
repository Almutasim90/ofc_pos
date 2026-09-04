# Sprint 06 — Payments

## Sprint Goal
تنفيذ وسائل الدفع والدفع المجزأ ودورة الدفع.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-06-01: Dynamic payment methods
- STORY-06-02: Cash
- STORY-06-03: OmanNet/Card
- STORY-06-04: Split payments
- STORY-06-05: Payment lifecycle
- STORY-06-06: Payment validation
- STORY-06-07: Receipt payment breakdown

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
- Payment totals must equal order total
- Split payment works
- Failed payment does not post financial sale
- Posted transaction is immutable

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
