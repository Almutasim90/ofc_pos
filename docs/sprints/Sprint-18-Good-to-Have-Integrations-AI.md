# Sprint 18 — Good-to-Have Integrations & AI

## Sprint Goal
إضافة الميزات غير الأساسية بعد استقرار النواة.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-18-01: Loyalty
- STORY-18-02: Customer CRM
- STORY-18-03: Online ordering
- STORY-18-04: Delivery adapters
- STORY-18-05: Notifications
- STORY-18-06: AI forecasting
- STORY-18-07: AI upselling
- STORY-18-08: AI management insights
- STORY-18-09: SaaS productization readiness

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
- Core POS works if all optional services fail
- AI does not post irreversible decisions automatically
- Integrations use adapters and can be disabled

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
