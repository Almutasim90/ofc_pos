# Sprint 16 — QR Ordering & Customer Flow

## Sprint Goal
إضافة طلب العميل عبر QR باستخدام نفس محرك النظام.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-16-01: QR context
- STORY-16-02: Customer menu
- STORY-16-03: Modifiers/combos
- STORY-16-04: Submit order
- STORY-16-05: Approval mode
- STORY-16-06: Live tracking
- STORY-16-07: Branch/table/parking context

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
- QR uses same pricing/order engine
- Customer sees live state
- Unavailable items cannot be ordered

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
