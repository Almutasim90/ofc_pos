# Sprint 04 — Pricing, Channels & Tax

## Sprint Goal
تطبيق محرك الأسعار والقنوات والضريبة مع Price Snapshot.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-04-01: Sales channels
- STORY-04-02: Payment-method separation
- STORY-04-03: Price hierarchy
- STORY-04-04: Branch/channel prices
- STORY-04-05: Promotions foundation
- STORY-04-06: Tax inclusive/exclusive
- STORY-04-07: Effective dates
- STORY-04-08: Price/Tax snapshot
- STORY-04-09: Offline Price Lock

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
- Cloud price change does not alter offline completed order
- Old invoices keep original tax snapshot
- Correct channel price is resolved
- Manual override requires permission and audit

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
