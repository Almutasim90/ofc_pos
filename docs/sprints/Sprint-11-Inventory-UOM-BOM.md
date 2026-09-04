# Sprint 11 — Inventory, UOM & BOM

## Sprint Goal
تأسيس المواد الخام والوحدات والوصفات والحركات المخزنية.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-11-01: Inventory items
- STORY-11-02: Units of measure
- STORY-11-03: Unit conversion
- STORY-11-04: Recipes/BOM
- STORY-11-05: Recipe versioning
- STORY-11-06: Inventory ledger
- STORY-11-07: Sale deduction
- STORY-11-08: Stock balance projection

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
- Sale creates correct ingredient deductions
- Historical recipe remains unchanged
- Unit conversions are accurate
- Ledger can rebuild current balance

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
