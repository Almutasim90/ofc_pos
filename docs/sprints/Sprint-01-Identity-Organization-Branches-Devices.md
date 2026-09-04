# Sprint 01 — Identity, Organization, Branches & Devices

## Sprint Goal
تأسيس المستخدمين والفروع وأجهزة POS والصلاحيات الأساسية.

## Master References
- `00-MASTER-SRS.md`
- `01-ARCHITECTURE-GUARDRAILS.md`
- `02-UI-DESIGN-SYSTEM.md`

## Stories
- STORY-01-01: Users and authentication
- STORY-01-02: Roles and granular permissions
- STORY-01-03: Organization/Branch model
- STORY-01-04: POS Device registration
- STORY-01-05: Device status/last seen
- STORY-01-06: Branch-level settings

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
- Cashier can login to assigned branch/device
- Unauthorized operations are blocked server-side
- Admin can manage branches/devices
- Audit records security-sensitive changes

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
