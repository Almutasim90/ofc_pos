# Sprint 02 — Catalog

## Sprint Goal
بناء الكتالوج الأساسي للأصناف والتصنيفات والصور والتوفر.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-02-01: Categories/subcategories
- STORY-02-02: Products bilingual fields
- STORY-02-03: SKU/barcode
- STORY-02-04: Images
- STORY-02-05: Product types
- STORY-02-06: Branch availability
- STORY-02-07: Preparation station reference

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
- Product appears in correct category
- Arabic/English names render correctly
- Inactive product cannot be sold
- Catalog works responsively

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
