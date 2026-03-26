# IAET Phase 6: Explorer — Local Web UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local Swagger-like web UI (`iaet explore`) for browsing captured endpoints, viewing request/response pairs, viewing inferred schemas, replaying requests, and triggering exports.

**Architecture:** ASP.NET Core Minimal API backend serving JSON endpoints, with Razor Pages for the UI. The backend consumes `IEndpointCatalog`, `IStreamCatalog`, `ISchemaInferrer`, `IReplayEngine`, and the Export generators. Serves on `http://localhost:9200` by default. Functional, not fancy.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, Razor Pages, System.Text.Json, xUnit + FluentAssertions + WebApplicationFactory

**Spec:** See design spec Section 5.5

**IMPORTANT:** All work on branch `phase6-explorer`. Create PR to main when complete. Run comprehensive code review before merging.

---

## Phase 6 Scope

By the end of this phase:
- ASP.NET Core app with Minimal API endpoints for session/endpoint/stream/schema data
- Razor Pages UI: sessions list, endpoint browser, request/response viewer, schema viewer, stream viewer
- Interactive replay from the UI (POST to replay API, view diff)
- Export controls (trigger any export format from UI, download result)
- `iaet explore --db <path> --port <port>` CLI command
- Integration tests via WebApplicationFactory
- `Iaet.Explorer.ServiceCollectionExtensions` for DI

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `src/Iaet.Explorer/Iaet.Explorer.csproj` | Modify | Add ASP.NET Core + Razor packages |
| `src/Iaet.Explorer/ExplorerApp.cs` | Create | WebApplication builder + configuration |
| `src/Iaet.Explorer/Api/SessionsApi.cs` | Create | GET /api/sessions, GET /api/sessions/{id} |
| `src/Iaet.Explorer/Api/EndpointsApi.cs` | Create | GET /api/sessions/{id}/endpoints, GET /api/sessions/{id}/endpoints/{sig}/requests |
| `src/Iaet.Explorer/Api/StreamsApi.cs` | Create | GET /api/sessions/{id}/streams |
| `src/Iaet.Explorer/Api/SchemaApi.cs` | Create | GET /api/sessions/{id}/endpoints/{sig}/schema |
| `src/Iaet.Explorer/Api/ReplayApi.cs` | Create | POST /api/replay/{requestId} |
| `src/Iaet.Explorer/Api/ExportApi.cs` | Create | GET /api/sessions/{id}/export/{format} |
| `src/Iaet.Explorer/Pages/Index.cshtml` | Create | Sessions list page |
| `src/Iaet.Explorer/Pages/Session.cshtml` | Create | Endpoint browser for a session |
| `src/Iaet.Explorer/Pages/Endpoint.cshtml` | Create | Request/response viewer + schema |
| `src/Iaet.Explorer/Pages/Streams.cshtml` | Create | Stream viewer |
| `src/Iaet.Explorer/Pages/Shared/_Layout.cshtml` | Create | Shared layout with nav |
| `src/Iaet.Explorer/wwwroot/css/site.css` | Create | Minimal CSS |
| `src/Iaet.Explorer/ServiceCollectionExtensions.cs` | Create | DI registration |
| `src/Iaet.Cli/Commands/ExploreCommand.cs` | Create | CLI command |
| `tests/Iaet.Explorer.Tests/ApiTests.cs` | Create | WebApplicationFactory integration tests |

---

## Task 1: Create Branch + ASP.NET Core Project Setup

- [ ] **Step 1: Create branch**
```bash
cd D:/prj/IAET && git checkout main && git pull origin main
git checkout -b phase6-explorer
```

- [ ] **Step 2: Set up Explorer as ASP.NET Core project**

The existing `src/Iaet.Explorer/Iaet.Explorer.csproj` is a classlib. Convert it to a web project by changing the SDK and adding Razor Pages:

Replace `Sdk="Microsoft.NET.Sdk"` with `Sdk="Microsoft.NET.Sdk.Web"` in the csproj. Add package reference for Razor Pages runtime compilation if needed. The project already references Core, Catalog, Schema, Replay, Export.

Remove `Placeholder.cs`.

- [ ] **Step 3: Create ExplorerApp.cs** — the WebApplication builder

```csharp
namespace Iaet.Explorer;

public static class ExplorerApp
{
    public static WebApplication Build(string dbPath, int port = 9200)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorPages();

        // Register IAET services
        builder.Services.AddIaetCatalog($"DataSource={dbPath}");
        builder.Services.AddIaetSchema();
        builder.Services.AddIaetReplay();
        builder.Services.AddIaetExport();

        builder.WebHost.UseUrls($"http://localhost:{port}");

        var app = builder.Build();
        app.UseStaticFiles();
        app.MapRazorPages();

        // Map API endpoints
        app.MapSessionsApi();
        app.MapEndpointsApi();
        app.MapStreamsApi();
        app.MapSchemaApi();
        app.MapReplayApi();
        app.MapExportApi();

        return app;
    }
}
```

- [ ] **Step 4: Create ServiceCollectionExtensions**
```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetExplorer(this IServiceCollection services)
    {
        return services;
    }
}
```

- [ ] **Step 5: Build and commit**
```bash
dotnet build src/Iaet.Explorer
git add src/Iaet.Explorer/
git commit -m "feat: set up Explorer as ASP.NET Core web app with ExplorerApp builder"
```

---

## Task 2: JSON API Endpoints (TDD)

- [ ] **Step 1: Set up test project**
```bash
dotnet new xunit -n Iaet.Explorer.Tests -o tests/Iaet.Explorer.Tests
rm tests/Iaet.Explorer.Tests/UnitTest1.cs
dotnet sln Iaet.slnx add tests/Iaet.Explorer.Tests/Iaet.Explorer.Tests.csproj
dotnet add tests/Iaet.Explorer.Tests reference src/Iaet.Explorer
dotnet add tests/Iaet.Explorer.Tests reference src/Iaet.Core
dotnet add tests/Iaet.Explorer.Tests package FluentAssertions
dotnet add tests/Iaet.Explorer.Tests package NSubstitute
dotnet add tests/Iaet.Explorer.Tests package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 2: Create API endpoint files**

**SessionsApi.cs** — `GET /api/sessions` returns list, `GET /api/sessions/{id}` returns single
**EndpointsApi.cs** — `GET /api/sessions/{id}/endpoints` returns endpoint groups, `GET /api/sessions/{id}/endpoints/{sig}/requests` returns requests for that endpoint
**StreamsApi.cs** — `GET /api/sessions/{id}/streams` returns captured streams
**SchemaApi.cs** — `GET /api/sessions/{id}/endpoints/{sig}/schema` infers and returns schema
**ReplayApi.cs** — `POST /api/replay/{requestId}` replays a request and returns diff
**ExportApi.cs** — `GET /api/sessions/{id}/export/{format}` generates export (returns file download)

Each API file is a static class with `Map*Api(this WebApplication app)` extension methods using Minimal API.

- [ ] **Step 3: Write integration tests via WebApplicationFactory**

Tests:
- GetSessions_ReturnsOk
- GetEndpoints_ReturnsOk
- GetStreams_ReturnsOk
- GetSchema_ReturnsOk
- PostReplay_ReturnsResult
- GetExport_ReturnsFile

Use in-memory SQLite database seeded with test data.

- [ ] **Step 4: Commit**
```bash
git add src/Iaet.Explorer/Api/ tests/Iaet.Explorer.Tests/
git commit -m "feat: add Explorer JSON API endpoints with integration tests"
```

---

## Task 3: Razor Pages UI

- [ ] **Step 1: Create shared layout**

`Pages/Shared/_Layout.cshtml` — minimal HTML layout with nav bar (Sessions, About), CSS link.

- [ ] **Step 2: Create pages**

**Pages/Index.cshtml** — lists sessions in a table (name, target, date, request count). Links to session detail.

**Pages/Session.cshtml** — shows endpoint groups table for a session. Click an endpoint to see details. Shows streams if any.

**Pages/Endpoint.cshtml** — shows all requests for an endpoint. First request expanded with headers and body. Shows inferred schema (C# record + JSON Schema). Has "Replay" button.

**Pages/Streams.cshtml** — stream details with metadata and frame history.

Each page uses `@page` directive with route parameters and `@inject` for services.

- [ ] **Step 3: Create minimal CSS**

`wwwroot/css/site.css` — basic styling for tables, code blocks, nav, and layout. Functional, not fancy.

- [ ] **Step 4: Commit**
```bash
git add src/Iaet.Explorer/Pages/ src/Iaet.Explorer/wwwroot/
git commit -m "feat: add Razor Pages UI for sessions, endpoints, schemas, and streams"
```

---

## Task 4: CLI Command + Docs + PR

- [ ] **Step 1: Create ExploreCommand**

```bash
dotnet add src/Iaet.Cli reference src/Iaet.Explorer
```

`iaet explore --db <path> --port <port>`:
1. Builds ExplorerApp with the specified db path and port
2. Prints URL to console
3. Opens browser (optional, via `Process.Start`)
4. Runs until Ctrl+C

- [ ] **Step 2: Register in Program.cs**
```csharp
services.AddIaetExplorer();
ExploreCommand.Create(host.Services)
```

- [ ] **Step 3: Update README + Explorer README**

Move Explorer from "coming" to implemented. Add usage examples.

- [ ] **Step 4: Run full test suite, push, create PR**
```bash
dotnet test Iaet.slnx -c Release
git push origin phase6-explorer
gh pr create --title "Phase 6: Explorer — Local Swagger-like Web UI" --body "..."
```

---

## What's Next
Phase 7 (Browser Extensions), Phase 8 (Investigation Wizard), Phase 9 (GVResearch Update).
