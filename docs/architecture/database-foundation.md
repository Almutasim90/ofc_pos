# PostgreSQL and EF Core Foundation

## Scope

STORY-00-04 configures EF Core with the Npgsql PostgreSQL provider and establishes the application-owned `ofc` schema. No business entities, tables, seed data, or financial data are introduced.

## Connection Configuration

Set the direct PostgreSQL connection string outside source control using `ConnectionStrings__OFC`. The application fails at startup if it is absent rather than silently operating without a database configuration.

Do not use Supabase API keys, JWT signing keys, dashboard credentials, or encryption keys as a database connection string. The connection must be a PostgreSQL URI or Npgsql connection string for a dedicated application database role.

## Schema Ownership

- OFC application tables are stored in the `ofc` schema.
- EF Core migration history remains in PostgreSQL's default schema so the initial migration can create `ofc` on a clean database.
- Supabase-managed schemas such as `auth`, `storage`, and `realtime` are not modified by OFC migrations.
- PostgreSQL remains the final system of record.

## Migration Commands

```text
dotnet ef migrations add <MigrationName> --project backend/src/OFC.Infrastructure --startup-project backend/src/OFC.Api --output-dir Persistence/Migrations
dotnet ef database update --project backend/src/OFC.Infrastructure --startup-project backend/src/OFC.Api
```

Run the update command only after exporting a rotated direct connection string as `ConnectionStrings__OFC` in the local shell or deployment environment.
