# Sprint 15 — Advanced Inventory, Waste & Costing

## Sprint Goal
إضافة الجرد والتحويل والهدر والتكلفة.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-15-01: Physical counts
- STORY-15-02: Count variance
- STORY-15-03: Stock transfer
- STORY-15-04: Waste categories
- STORY-15-05: Cancelled-order waste
- STORY-15-06: Weighted average cost
- STORY-15-07: Recipe cost
- STORY-15-08: Food cost %
- STORY-15-09: Gross margin

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
- Transfer does not increase destination before receipt
- Waste is auditable
- Count adjustment requires reason/approval
- Recipe cost is reproducible

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
