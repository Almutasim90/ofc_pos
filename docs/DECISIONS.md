# Architecture Decisions

## ADR-0001: Isolated Repository Root

**Status:** Accepted

The OFC project directory is its own Git repository. This prevents unrelated user-home files from entering status checks, commits, or CI scopes.

## ADR-0002: Canonical Documentation Path

**Status:** Accepted

All product, architecture, sprint, and project-state documentation resides under `docs/`. This matches the delivery instructions and provides a stable handoff path.

## ADR-0003: Reserved Application Boundaries

**Status:** Accepted

`apps/web` is reserved for the React application. `backend/src` and `backend/tests` are reserved for the ASP.NET Core modular monolith and its tests. STORY-00-01 introduces no executable application code.

## ADR-0004: .NET 10 Modular Monolith Baseline

**Status:** Accepted

The backend targets .NET 10 LTS. `OFC.Api` is the single deployable host; it composes `OFC.Infrastructure`, which may depend on `OFC.SharedKernel`. Business modules are introduced only in their approved story and cannot depend on `OFC.Api`.

## ADR-0005: Vite React Frontend Baseline

**Status:** Accepted

The frontend uses Vite, React, TypeScript, and Tailwind 4. shadcn/ui is configured with path aliases and its standard class-composition utility, but components and visual tokens are added only when their assigned story requires them.

## ADR-0006: OFC PostgreSQL Schema Boundary

**Status:** Accepted

OFC uses EF Core with the Npgsql provider and stores application-owned tables in the `ofc` PostgreSQL schema. EF Core migration history remains in the default schema because it must exist before the initial migration can create `ofc`. Supabase-managed schemas are outside OFC migration ownership.
