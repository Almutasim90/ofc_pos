# Sprint 05 — POS & Unified Ordering

## Sprint Goal
إنشاء شاشة POS الحديثة ومحرك الطلب الموحد.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-05-01: POS responsive layout
- STORY-05-02: Category/product browsing
- STORY-05-03: Search
- STORY-05-04: Cart
- STORY-05-05: Notes
- STORY-05-06: Hold/resume
- STORY-05-07: Order state machine
- STORY-05-08: Order status history
- STORY-05-09: Unified order source model

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
- Simple product add = 1 click after category
- Cart remains usable offline
- Order status transitions validated
- POS works at desktop/tablet/mobile widths

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
