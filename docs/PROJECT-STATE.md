# Project State

## Current Sprint

Sprint 00 - Engineering Foundation

## Completed Stories

- STORY-00-01: Repository and solution structure
- STORY-00-02: ASP.NET Core Modular Monolith Skeleton
- STORY-00-03: React/TypeScript/Tailwind/shadcn setup
- STORY-00-04: PostgreSQL + EF migrations
- STORY-00-05: Localization AR/EN + RTL/LTR
- STORY-00-06: Responsive app shell
- STORY-00-07: Authentication foundation
- STORY-00-08: ProblemDetails + logging + correlation IDs
- STORY-00-09: Testing + CI foundation
- STORY-00-10: Offline-compatible IDs and local-store foundation

## Current Story

Sprint 00 review gate.

## Implemented Modules

- `OFC.Api`: single ASP.NET Core API host with a health endpoint.
- `OFC.Infrastructure`: technical composition assembly.
- `OFC.SharedKernel`: business-neutral shared assembly.
- `Modules`: reserved for approved business modules.
- `apps/web`: Vite React TypeScript application with Tailwind 4 and shadcn/ui prerequisites.
- PostgreSQL EF Core foundation: Npgsql provider, `OFCDbContext`, and the applied initial `ofc` schema migration.
- API foundation: ProblemDetails, correlation IDs, structured logging scope, health checks, and authentication/authorization middleware.
- Frontend foundation: Arabic/English responsive shell, RTL/LTR switching, service status states, and namespaced local storage.
- Test and CI foundation: xUnit test project plus GitHub Actions build/test/type-check workflow.

## Latest Migration

`20260904092805_InitializeOFCApplicationSchema` (applied to the development database).

## Architecture Decisions

- The OFC project is an isolated Git repository rooted at this directory.
- Product and delivery documentation is canonical under `docs/`.
- Application code is reserved for `apps/web/` and `backend/src/`; tests belong in `backend/tests/`.
- No business modules or runtime dependencies are introduced by STORY-00-01.
- The backend targets .NET 10 LTS and uses a modular-monolith dependency direction.
- The frontend uses Vite, React, TypeScript, Tailwind 4, and shadcn/ui conventions.
- OFC-owned database objects use the `ofc` schema; the EF migration-history table remains in PostgreSQL's default schema for bootstrap safety.

## Known Issues

- Database credentials disclosed in chat must be rotated. Local development accesses PostgreSQL through an SSH tunnel.

## Next Recommended Story

Start Sprint 01: Identity, Organization, Branches & Devices.

## Do-Not-Change Rules

- Do not implement Sprint 01 or later business features during Sprint 00.
- Keep PostgreSQL as the final source of truth and IndexedDB as an operational local store.
- Preserve Arabic RTL and English LTR as cross-cutting requirements from the first UI implementation.
- Do not add business logic to React components or API controllers.
