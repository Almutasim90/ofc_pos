# Backend Structure

The OFC backend is a modular monolith hosted by one ASP.NET Core API process.

## Assemblies

- `OFC.Api` composes the application and exposes HTTP endpoints.
- `OFC.SharedKernel` contains narrowly shared, business-neutral primitives.
- `OFC.Infrastructure` contains technical implementations used by the API and modules.
- `Modules/` will contain business modules when their assigned sprint begins.

## Dependency Direction

```text
OFC.Api -> OFC.Infrastructure -> OFC.SharedKernel
OFC.Api -> Modules/* -> OFC.SharedKernel
OFC.Infrastructure -> Modules/* only through explicitly introduced abstractions
```

Business modules must not depend on `OFC.Api`, and cross-module access must use explicit contracts rather than another module's internal implementation.

## Current Operational Endpoint

- `GET /health` returns the API health status. It is intentionally unauthenticated for infrastructure probes.

## Database Configuration

The API reads its PostgreSQL connection string from `ConnectionStrings__OFC`. Do not store connection strings, passwords, JWTs, service keys, or encryption keys in tracked configuration files. The initial migration creates the application-owned `ofc` schema; EF Core keeps its migration-history table in PostgreSQL's default schema so the first deployment can create `ofc` safely.

Generate a migration from `backend/`:

```text
dotnet ef migrations add <MigrationName> --project src/OFC.Infrastructure --startup-project src/OFC.Api --output-dir Persistence/Migrations
```

Apply migrations only with `ConnectionStrings__OFC` set to a rotated, direct PostgreSQL connection string:

```text
dotnet ef database update --project src/OFC.Infrastructure --startup-project src/OFC.Api
```
