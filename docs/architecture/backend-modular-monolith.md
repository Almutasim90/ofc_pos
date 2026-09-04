# Backend Modular Monolith Skeleton

## Scope

STORY-00-02 establishes the ASP.NET Core solution and assembly dependency direction. It does not introduce business modules, database persistence, authentication, authorization policies, or business endpoints.

## Solution

`backend/OFC.slnx` contains:

- `OFC.Api`: the single deployable ASP.NET Core Web API host.
- `OFC.Infrastructure`: technical composition point for future persistence and integrations.
- `OFC.SharedKernel`: narrowly shared, business-neutral primitives.

Future business modules are created under `backend/src/Modules` only in their approved delivery story.

## API Contract

| Method | Path | Authentication | Response |
| --- | --- | --- | --- |
| GET | `/health` | None | `200 OK` when the API host is healthy |

The health endpoint is reserved for infrastructure probes. Detailed error contracts, correlation IDs, logging conventions, and authenticated APIs are deferred to STORY-00-07 and STORY-00-08.
