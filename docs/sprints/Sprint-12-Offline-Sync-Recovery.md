# Sprint 12 — Offline Sync & Recovery

## Sprint Goal
إكمال المزامنة الآمنة وIdempotency وحالات الفشل.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-12-01: Local transaction queue
- STORY-12-02: Stable idempotency keys
- STORY-12-03: Retry/backoff
- STORY-12-04: Conflict states
- STORY-12-05: Server acknowledgement
- STORY-12-06: Recovery UI
- STORY-12-07: Stale pricing warnings
- STORY-12-08: Negative-stock discrepancy handling

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
- Repeated same key creates one business transaction
- Offline sale syncs without repricing
- Network failure does not lose sale
- Sync conflict is visible and actionable

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
