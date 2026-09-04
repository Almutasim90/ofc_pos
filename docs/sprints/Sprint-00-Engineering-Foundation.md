# Sprint 00 — Engineering Foundation

## Sprint Goal
تأسيس المشروع والمعمارية وتجربة الواجهة المشتركة قبل ميزات الأعمال.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-00-01: Repository & solution structure
- STORY-00-02: ASP.NET Core Modular Monolith skeleton
- STORY-00-03: React/TypeScript/Tailwind/shadcn setup
- STORY-00-04: PostgreSQL + EF migrations
- STORY-00-05: Localization AR/EN + RTL/LTR
- STORY-00-06: Responsive app shells
- STORY-00-07: Authentication foundation
- STORY-00-08: ProblemDetails + logging + correlation IDs
- STORY-00-09: Testing + CI foundation
- STORY-00-10: Offline-compatible IDs and local-store foundation

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
- Project builds in clean environment
- Arabic and English switch correctly
- RTL/LTR layouts pass smoke test
- API and frontend health checks pass
- CI runs build and tests

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
