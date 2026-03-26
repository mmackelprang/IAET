# Iaet.Catalog

`Iaet.Catalog` is the persistence layer. It stores capture sessions, raw HTTP requests, and deduplicated endpoint groups in a local SQLite database using [EF Core](https://learn.microsoft.com/en-us/ef/core/).

## SqliteCatalog

`SqliteCatalog` implements `IEndpointCatalog`. On `SaveRequestAsync` it:

1. Calls `EndpointNormalizer.Normalize` to produce a canonical `METHOD /path/{id}` signature.
2. Inserts the raw `CapturedRequest` entity.
3. Upserts the corresponding `EndpointGroup` row — incrementing `ObservationCount` and updating `LastSeen` if the signature already exists for this session, or creating a new group if not.

This deduplication happens at write time so `GetEndpointGroupsAsync` can return a ranked list instantly without a scan-and-group query.

## EF Core and Migrations

`CatalogDbContext` is a standard EF Core `DbContext` targeting SQLite. The CLI calls `db.Database.MigrateAsync()` at startup so the schema is always up to date. Migration files live in `Migrations/` and are compiled into the assembly, requiring no separate tooling at runtime.

`CatalogDbContextFactory` implements `IDesignTimeDbContextFactory` for EF Core tooling (`dotnet ef migrations add`, etc.) when no DI container is available.

## EndpointNormalizer

`EndpointNormalizer.Normalize` strips the query string, extracts the `AbsolutePath`, and delegates to `EndpointSignature.FromRequest`, which replaces numeric IDs, GUIDs, and long hex strings in path segments with the placeholder `{id}`. For example:

```
GET /api/users/12345/messages/abc123def456  →  GET /api/users/{id}/messages/{id}
```

## DI Registration

```csharp
services.AddIaetCatalog("DataSource=catalog.db");
```

This registers `CatalogDbContext`, `SqliteCatalog` as `IEndpointCatalog`, and scopes both to the DI container lifetime.
