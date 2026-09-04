# Sprint 09 — Printing & Local Hardware

## Sprint Goal
ضمان الطباعة المحلية المستقرة وعزل المتصفح عن الأجهزة.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-09-01: Local Print Agent contract
- STORY-09-02: Receipt print queue
- STORY-09-03: Kitchen print queue
- STORY-09-04: Cash drawer
- STORY-09-05: Printer routing
- STORY-09-06: Retry handling
- STORY-09-07: Printer health checks

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
- Receipt prints without cloud dependency
- Failed job is retryable
- No duplicate print on retry
- Product routes through station, not hard-coded printer

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
