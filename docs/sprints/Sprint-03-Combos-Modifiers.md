# Sprint 03 — Combos & Modifiers

## Sprint Goal
تمكين الوجبات المركبة والإضافات بصورة قابلة لإعادة الاستخدام.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-03-01: Combo groups
- STORY-03-02: Required/optional selections
- STORY-03-03: Min/max rules
- STORY-03-04: Price adjustments
- STORY-03-05: Modifier groups
- STORY-03-06: Default modifiers
- STORY-03-07: Branch availability

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
- Invalid combo cannot be added
- Price adjustments calculate correctly
- Common combo flow <=4 interactions
- Selections persist in order item snapshot

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
