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

## ADR-0007: Sprint 18 Extension Points Live in One Integrations Module

**Status:** Accepted

The SRS names `Notifications`, `Integrations`, and `AI` as separate modules. For the good-to-have Sprint 18, the adapter contracts, the notification/webhook outbox foundation, and the AI-assist extension point are hosted in a single `OFC.Modules.Integrations` assembly (`IntegrationModels`, `IntegrationRules`, `IExternalIntegrationAdapter`, `IAiAssistProvider`, `ExternalOutboxEntry`). Core POS/ordering never references these. This avoids introducing several near-empty projects and a heavy dependency graph before any real provider exists. If a real notification or AI provider is adopted later, its concrete implementation can move into the module it belongs in (or a dedicated provider assembly) without touching the contracts.

## ADR-0008: Optional Services Are Disabled By Default and Never On The Critical Path

**Status:** Accepted

Sprint 18 extension points are dormant until enabled, read from the existing `branch_settings` table at query time, and only durable state introduced in Sprint 18 is the `external_outbox` table, which is required for a reliable outbound notification/webhook foundation. Every adapter call is wrapped so an exception produces a recorded `Skipped`/`Failed`/`Deferred`/degraded outcome rather than propagating. The AI provider never posts a decision: `IntegrationRules.SuggestionAcceptsWithoutReview` always returns `false`. This keeps the acceptance criteria that core POS works if all optional services fail and that AI does not post irreversible decisions automatically.
