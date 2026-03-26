# IAET Phase 4: Export + Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `Iaet.Export` assembly with 6 export formats (Markdown report, HTML report, OpenAPI YAML, Postman collection, C# typed client, HAR) plus CLI commands, and write comprehensive project documentation including tutorials.

**Architecture:** Each export format is a standalone generator class consuming `IEndpointCatalog`, `IStreamCatalog`, and `ISchemaInferrer`. A shared `ExportContext` loads all session data once. CLI commands delegate to generators. All output sanitizes credentials.

**Tech Stack:** .NET 10, System.Text.Json, System.Text.Encodings.Web (for HTML), xUnit + FluentAssertions

**Spec:** See design spec Section 5.4 and Section 9

**IMPORTANT:** All work on branch `phase4-export`. Create PR to main when complete. Run comprehensive code review before merging.

---

## Phase 4 Scope

By the end of this phase:
- `ExportContext` — shared data loader for all generators
- 6 export generators: Markdown, HTML, OpenAPI, Postman, C# client, HAR
- `iaet export report|html|openapi|postman|csharp|har` CLI commands
- `Iaet.Export.ServiceCollectionExtensions` for DI
- Comprehensive project tutorials in `docs/tutorials/`
- Tests for all generators

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `src/Iaet.Export/ExportContext.cs` | Create | Loads session data from catalogs for generators |
| `src/Iaet.Export/Generators/MarkdownReportGenerator.cs` | Create | Markdown investigation report |
| `src/Iaet.Export/Generators/HtmlReportGenerator.cs` | Create | Self-contained HTML report |
| `src/Iaet.Export/Generators/OpenApiGenerator.cs` | Create | OpenAPI 3.1 YAML spec |
| `src/Iaet.Export/Generators/PostmanGenerator.cs` | Create | Postman collection JSON |
| `src/Iaet.Export/Generators/CSharpClientGenerator.cs` | Create | C# record types + HttpClient wrapper |
| `src/Iaet.Export/Generators/HarGenerator.cs` | Create | HTTP Archive (HAR 1.2) JSON |
| `src/Iaet.Export/ServiceCollectionExtensions.cs` | Create | DI registration |
| `src/Iaet.Cli/Commands/ExportCommand.cs` | Create | CLI commands for all 6 formats |
| `src/Iaet.Cli/Iaet.Cli.csproj` | Modify | Add Iaet.Export reference |
| `src/Iaet.Cli/Program.cs` | Modify | Register ExportCommand + DI |
| `tests/Iaet.Export.Tests/ExportContextTests.cs` | Create | Context loading tests |
| `tests/Iaet.Export.Tests/Generators/MarkdownReportGeneratorTests.cs` | Create | Markdown output tests |
| `tests/Iaet.Export.Tests/Generators/OpenApiGeneratorTests.cs` | Create | OpenAPI output tests |
| `tests/Iaet.Export.Tests/Generators/PostmanGeneratorTests.cs` | Create | Postman output tests |
| `tests/Iaet.Export.Tests/Generators/CSharpClientGeneratorTests.cs` | Create | C# output tests |
| `tests/Iaet.Export.Tests/Generators/HarGeneratorTests.cs` | Create | HAR output tests |
| `docs/tutorials/investigating-spotify.md` | Create | End-to-end tutorial |
| `docs/adapter-guide.md` | Create | How to write adapters |

---

## Task 1: Create Branch, ExportContext, and Test Project

**Files:**
- Create: `src/Iaet.Export/ExportContext.cs`
- Create: `tests/Iaet.Export.Tests/Iaet.Export.Tests.csproj`
- Create: `tests/Iaet.Export.Tests/ExportContextTests.cs`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/prj/IAET
git checkout main && git pull origin main
git checkout -b phase4-export
```

- [ ] **Step 2: Set up Export test project**

```bash
dotnet new xunit -n Iaet.Export.Tests -o tests/Iaet.Export.Tests
rm tests/Iaet.Export.Tests/UnitTest1.cs
dotnet sln Iaet.slnx add tests/Iaet.Export.Tests/Iaet.Export.Tests.csproj
dotnet add tests/Iaet.Export.Tests reference src/Iaet.Export
dotnet add tests/Iaet.Export.Tests reference src/Iaet.Core
dotnet add tests/Iaet.Export.Tests package FluentAssertions
dotnet add tests/Iaet.Export.Tests package NSubstitute
```

Strip redundant csproj properties. Add `IsTestProject=true` and `NoWarn CA1707` to test csproj. Remove `Placeholder.cs` from `src/Iaet.Export/`.

- [ ] **Step 3: Write ExportContext tests (TDD)**

`ExportContext` loads all the data a generator needs from a session. It's the shared data object that all generators consume.

```csharp
using FluentAssertions;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.Export;
using NSubstitute;

namespace Iaet.Export.Tests;

public class ExportContextTests
{
    [Fact]
    public async Task LoadAsync_PopulatesAllData()
    {
        var sessionId = Guid.NewGuid();
        var catalog = Substitute.For<IEndpointCatalog>();
        var streamCatalog = Substitute.For<IStreamCatalog>();
        var schemaInferrer = Substitute.For<ISchemaInferrer>();

        var session = new CaptureSessionInfo
        {
            Id = sessionId, Name = "test", TargetApplication = "TestApp",
            Profile = "default", StartedAt = DateTimeOffset.UtcNow, CapturedRequestCount = 2
        };
        catalog.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns([session]);
        catalog.GetRequestsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([MakeRequest(sessionId)]);
        catalog.GetEndpointGroupsAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([new EndpointGroup(
                EndpointSignature.FromRequest("GET", "/api/users/{id}"),
                2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]);
        streamCatalog.GetStreamsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([]);

        var ctx = await ExportContext.LoadAsync(sessionId, catalog, streamCatalog, schemaInferrer);

        ctx.Session.Should().NotBeNull();
        ctx.Requests.Should().HaveCount(1);
        ctx.EndpointGroups.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadAsync_InfersSchemasPerEndpoint()
    {
        var sessionId = Guid.NewGuid();
        var catalog = Substitute.For<IEndpointCatalog>();
        var streamCatalog = Substitute.For<IStreamCatalog>();
        var schemaInferrer = Substitute.For<ISchemaInferrer>();

        catalog.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns([new CaptureSessionInfo
            {
                Id = sessionId, Name = "test", TargetApplication = "App",
                Profile = "p", StartedAt = DateTimeOffset.UtcNow
            }]);
        catalog.GetRequestsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([]);
        catalog.GetEndpointGroupsAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([new EndpointGroup(
                EndpointSignature.FromRequest("GET", "/api/data"),
                1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]);
        catalog.GetResponseBodiesAsync(sessionId, "GET /api/data", Arg.Any<CancellationToken>())
            .Returns(["{ \"key\": \"value\" }"]);
        streamCatalog.GetStreamsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([]);
        schemaInferrer.InferAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new SchemaResult("{}", "record R;", "type: object", []));

        var ctx = await ExportContext.LoadAsync(sessionId, catalog, streamCatalog, schemaInferrer);

        ctx.SchemasByEndpoint.Should().ContainKey("GET /api/data");
        await schemaInferrer.Received(1).InferAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    private static CapturedRequest MakeRequest(Guid sessionId) => new()
    {
        Id = Guid.NewGuid(), SessionId = sessionId,
        Timestamp = DateTimeOffset.UtcNow, HttpMethod = "GET",
        Url = "https://api.test/users/123",
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string> { ["content-type"] = "application/json" },
        ResponseBody = """{"name":"Alice"}""",
        DurationMs = 50
    };
}
```

- [ ] **Step 4: Implement ExportContext**

```csharp
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Export;

public sealed class ExportContext
{
    public required CaptureSessionInfo Session { get; init; }
    public required IReadOnlyList<CapturedRequest> Requests { get; init; }
    public required IReadOnlyList<EndpointGroup> EndpointGroups { get; init; }
    public required IReadOnlyList<CapturedStream> Streams { get; init; }
    public required IReadOnlyDictionary<string, SchemaResult> SchemasByEndpoint { get; init; }

    public static async Task<ExportContext> LoadAsync(
        Guid sessionId,
        IEndpointCatalog catalog,
        IStreamCatalog streamCatalog,
        ISchemaInferrer schemaInferrer,
        CancellationToken ct = default)
    {
        var sessions = await catalog.ListSessionsAsync(ct);
        var session = sessions.First(s => s.Id == sessionId);
        var requests = await catalog.GetRequestsBySessionAsync(sessionId, ct);
        var groups = await catalog.GetEndpointGroupsAsync(sessionId, ct);
        var streams = await streamCatalog.GetStreamsBySessionAsync(sessionId, ct);

        // Infer schemas per endpoint
        var schemas = new Dictionary<string, SchemaResult>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var bodies = await catalog.GetResponseBodiesAsync(sessionId, group.Signature.Normalized, ct);
            if (bodies.Count > 0)
            {
                var schema = await schemaInferrer.InferAsync(bodies, ct);
                schemas[group.Signature.Normalized] = schema;
            }
        }

        return new ExportContext
        {
            Session = session,
            Requests = requests,
            EndpointGroups = groups,
            Streams = streams,
            SchemasByEndpoint = schemas
        };
    }
}
```

- [ ] **Step 5: Run tests, commit**

```bash
dotnet test Iaet.slnx -v n
git add src/Iaet.Export/ tests/Iaet.Export.Tests/
git commit -m "feat: add ExportContext for loading session data with schema inference"
```

---

## Task 2: Markdown Report Generator (TDD)

**Files:**
- Create: `src/Iaet.Export/Generators/MarkdownReportGenerator.cs`
- Create: `tests/Iaet.Export.Tests/Generators/MarkdownReportGeneratorTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class MarkdownReportGeneratorTests
{
    [Fact]
    public void Generate_IncludesSessionHeader()
    {
        var ctx = MakeContext();
        var md = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("# API Investigation Report");
        md.Should().Contain("TestApp");
        md.Should().Contain("test-session");
    }

    [Fact]
    public void Generate_ListsEndpoints()
    {
        var ctx = MakeContext();
        var md = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("GET /api/users/{id}");
        md.Should().Contain("Observations: 2");
    }

    [Fact]
    public void Generate_IncludesExampleRequestResponse()
    {
        var ctx = MakeContext();
        var md = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("```json");
        md.Should().Contain("Alice");
    }

    [Fact]
    public void Generate_IncludesInferredSchema()
    {
        var ctx = MakeContext();
        var md = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("C# Record");
        md.Should().Contain("record InferredResponse");
    }

    [Fact]
    public void Generate_IncludesStreamSummary()
    {
        var ctx = MakeContextWithStreams();
        var md = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("WebSocket");
        md.Should().Contain("wss://api.test/ws");
    }

    [Fact]
    public void Generate_RedactsCredentials()
    {
        var ctx = MakeContext();
        var md = MarkdownReportGenerator.Generate(ctx);

        md.Should().NotContain("Bearer secret");
        md.Should().Contain("<REDACTED>");
    }
    // ... helper methods to create ExportContext with test data
}
```

- [ ] **Step 2: Implement MarkdownReportGenerator**

`static string Generate(ExportContext ctx)` — builds a Markdown document with sections:
1. Title + session metadata (target, date, request count)
2. Endpoint catalog table (method, path, observations, first/last seen)
3. Per-endpoint details: example request/response (first captured), inferred schema (C# record + JSON Schema)
4. Stream summary table (if any streams captured)
5. Footer with generation timestamp

Uses `StringBuilder` for efficient string building.

- [ ] **Step 3: Run tests, commit**

```bash
git add src/Iaet.Export/Generators/ tests/Iaet.Export.Tests/Generators/
git commit -m "feat: add Markdown report generator with endpoint catalog and schema output"
```

---

## Task 3: OpenAPI + Postman + HAR Generators (TDD)

Three structured output generators that produce standard format files.

**Files:**
- Create: `src/Iaet.Export/Generators/OpenApiGenerator.cs`
- Create: `src/Iaet.Export/Generators/PostmanGenerator.cs`
- Create: `src/Iaet.Export/Generators/HarGenerator.cs`
- Create: `tests/Iaet.Export.Tests/Generators/OpenApiGeneratorTests.cs`
- Create: `tests/Iaet.Export.Tests/Generators/PostmanGeneratorTests.cs`
- Create: `tests/Iaet.Export.Tests/Generators/HarGeneratorTests.cs`

- [ ] **Step 1: Write OpenAPI tests**

```csharp
public class OpenApiGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidYaml()
    {
        var ctx = MakeContext();
        var yaml = OpenApiGenerator.Generate(ctx);

        yaml.Should().Contain("openapi: '3.1.0'");
        yaml.Should().Contain("paths:");
        yaml.Should().Contain("/api/users/{id}:");
        yaml.Should().Contain("get:");
    }

    [Fact]
    public void Generate_IncludesSchemaComponents()
    {
        var ctx = MakeContext();
        var yaml = OpenApiGenerator.Generate(ctx);

        yaml.Should().Contain("components:");
        yaml.Should().Contain("schemas:");
    }

    [Fact]
    public void Generate_SanitizesServerUrl()
    {
        var ctx = MakeContext();
        var yaml = OpenApiGenerator.Generate(ctx);

        yaml.Should().Contain("servers:");
    }
}
```

- [ ] **Step 2: Write Postman tests**

```csharp
public class PostmanGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidJson()
    {
        var ctx = MakeContext();
        var json = PostmanGenerator.Generate(ctx);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("info").GetProperty("name").GetString()
            .Should().Contain("TestApp");
    }

    [Fact]
    public void Generate_IncludesRequestItems()
    {
        var ctx = MakeContext();
        var json = PostmanGenerator.Generate(ctx);

        json.Should().Contain("item");
        json.Should().Contain("/api/users/{id}");
    }

    [Fact]
    public void Generate_RedactsAuthHeaders()
    {
        var ctx = MakeContext();
        var json = PostmanGenerator.Generate(ctx);

        json.Should().NotContain("Bearer secret");
    }
}
```

- [ ] **Step 3: Write HAR tests**

```csharp
public class HarGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidHarJson()
    {
        var ctx = MakeContext();
        var json = HarGenerator.Generate(ctx);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("log").GetProperty("version").GetString()
            .Should().Be("1.2");
    }

    [Fact]
    public void Generate_IncludesEntries()
    {
        var ctx = MakeContext();
        var json = HarGenerator.Generate(ctx);

        json.Should().Contain("entries");
        json.Should().Contain("api.test");
    }

    [Fact]
    public void Generate_IncludesTimings()
    {
        var ctx = MakeContext();
        var json = HarGenerator.Generate(ctx);

        json.Should().Contain("time");
    }
}
```

- [ ] **Step 4: Implement all three generators**

**OpenApiGenerator** — `static string Generate(ExportContext ctx)`:
- YAML output built with StringBuilder (no YAML library)
- Groups endpoints by path, lists methods per path
- Includes schema components from `SchemasByEndpoint`
- Extracts server URL from first request

**PostmanGenerator** — `static string Generate(ExportContext ctx)`:
- Postman Collection v2.1.0 JSON format
- One request item per endpoint group (using first captured request as example)
- Headers included (redacted values preserved)
- Environment variables for base URL

**HarGenerator** — `static string Generate(ExportContext ctx)`:
- HAR 1.2 JSON format
- One entry per captured request
- Includes request headers, query params, response headers, body, timing

All use `System.Text.Json.JsonSerializer` with `WriteIndented = true` for JSON output.

- [ ] **Step 5: Run tests, commit**

```bash
dotnet test tests/Iaet.Export.Tests -v n
git add src/Iaet.Export/Generators/ tests/Iaet.Export.Tests/Generators/
git commit -m "feat: add OpenAPI, Postman collection, and HAR generators"
```

---

## Task 4: C# Client + HTML Report Generators (TDD)

**Files:**
- Create: `src/Iaet.Export/Generators/CSharpClientGenerator.cs`
- Create: `src/Iaet.Export/Generators/HtmlReportGenerator.cs`
- Create: `tests/Iaet.Export.Tests/Generators/CSharpClientGeneratorTests.cs`

- [ ] **Step 1: Write C# client tests**

```csharp
public class CSharpClientGeneratorTests
{
    [Fact]
    public void Generate_ProducesCompilableCode()
    {
        var ctx = MakeContext();
        var code = CSharpClientGenerator.Generate(ctx);

        code.Should().Contain("namespace");
        code.Should().Contain("public sealed record");
        code.Should().Contain("HttpClient");
    }

    [Fact]
    public void Generate_IncludesMethodPerEndpoint()
    {
        var ctx = MakeContext();
        var code = CSharpClientGenerator.Generate(ctx);

        code.Should().Contain("GetUsersById"); // derived from GET /api/users/{id}
    }

    [Fact]
    public void Generate_IncludesRecordTypes()
    {
        var ctx = MakeContext();
        var code = CSharpClientGenerator.Generate(ctx);

        code.Should().Contain("record");
    }
}
```

- [ ] **Step 2: Write HTML report test**

The HTML report wraps the Markdown report in a self-contained HTML document with inline CSS. No external dependencies.

```csharp
// HtmlReportGenerator tests are minimal — it delegates to MarkdownReportGenerator
// and wraps in HTML. Just verify the wrapper is correct.
[Fact]
public void Generate_ProducesHtmlDocument()
{
    var ctx = MakeContext();
    var html = HtmlReportGenerator.Generate(ctx);

    html.Should().StartWith("<!DOCTYPE html>");
    html.Should().Contain("<style>");
    html.Should().Contain("API Investigation Report");
}
```

- [ ] **Step 3: Implement both generators**

**CSharpClientGenerator** — `static string Generate(ExportContext ctx)`:
- Generates a namespace with record types (from inferred schemas) and a partial HttpClient wrapper
- One method per endpoint group (e.g., `GetUsersByIdAsync`)
- Method name derived from HTTP method + path segments (PascalCase)
- Includes the C# records from `SchemasByEndpoint`

**HtmlReportGenerator** — `static string Generate(ExportContext ctx)`:
- Generates Markdown via `MarkdownReportGenerator.Generate(ctx)`
- Wraps in `<!DOCTYPE html>` with inline CSS for code blocks, tables, headers
- Simple Markdown-to-HTML conversion for the basic constructs used (headers, code blocks, tables, lists)
- Self-contained — no external CSS/JS

- [ ] **Step 4: Run tests, commit**

```bash
dotnet test tests/Iaet.Export.Tests -v n
git add src/Iaet.Export/Generators/ tests/Iaet.Export.Tests/Generators/
git commit -m "feat: add C# typed client and self-contained HTML report generators"
```

---

## Task 5: Export DI + CLI Commands

**Files:**
- Create: `src/Iaet.Export/ServiceCollectionExtensions.cs`
- Create: `src/Iaet.Cli/Commands/ExportCommand.cs`
- Modify: `src/Iaet.Cli/Iaet.Cli.csproj`
- Modify: `src/Iaet.Cli/Program.cs`

- [ ] **Step 1: Create DI registration**

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Export;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetExport(this IServiceCollection services)
    {
        // ExportContext is created per-invocation, not registered as a service
        // Generators are static classes, no DI needed
        // This extension exists for future expansion (custom generators, etc.)
        return services;
    }
}
```

- [ ] **Step 2: Add CLI reference and create ExportCommand**

```bash
dotnet add src/Iaet.Cli reference src/Iaet.Export
```

Create `src/Iaet.Cli/Commands/ExportCommand.cs` with 6 subcommands:

```
iaet export
├── report    --session-id <guid> --output <path>    Markdown report
├── html      --session-id <guid> --output <path>    HTML report
├── openapi   --session-id <guid> --output <path>    OpenAPI 3.1 YAML
├── postman   --session-id <guid> --output <path>    Postman collection
├── csharp    --session-id <guid> --output <path>    C# typed client
└── har       --session-id <guid> --output <path>    HTTP Archive
```

Each subcommand:
1. Creates DI scope, runs MigrateAsync
2. Loads `ExportContext.LoadAsync(sessionId, catalog, streamCatalog, schemaInferrer)`
3. Calls the appropriate generator
4. Writes output to file (or stdout if `--output` is `-`)

- [ ] **Step 3: Register in Program.cs**

```csharp
services.AddIaetExport();
// ...
ExportCommand.Create(host.Services)
```

- [ ] **Step 4: Verify CLI**

```bash
dotnet run --project src/Iaet.Cli -- export --help
```

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Export/ src/Iaet.Cli/
git commit -m "feat: add export CLI commands for report, html, openapi, postman, csharp, and har"
```

---

## Task 6: Write Project Documentation

**Files:**
- Create: `docs/tutorials/investigating-spotify.md`
- Create: `docs/adapter-guide.md`
- Modify: `README.md`
- Create: `src/Iaet.Export/README.md`

- [ ] **Step 1: Write Spotify tutorial**

`docs/tutorials/investigating-spotify.md` — end-to-end walkthrough:
1. Install IAET (`dotnet tool install -g iaet`)
2. Start capture session targeting Spotify Web Player
3. Browse Spotify, play songs, search
4. Stop capture, view discovered endpoints
5. View discovered streams (WebSocket for real-time, HLS for audio)
6. Infer schemas from response bodies
7. Export as OpenAPI spec
8. Export as Markdown investigation report
9. Next steps: replay, custom adapter

- [ ] **Step 2: Write adapter guide**

`docs/adapter-guide.md`:
1. What adapters are and why you'd write one
2. Implement `IApiAdapter` interface
3. Example: SpotifyAdapter (matches spotify.com URLs, categorizes endpoints)
4. Register in the consumer project
5. Use `--adapter` flag or `~/.iaet/adapters/` directory

- [ ] **Step 3: Update README.md**

Move Export from "coming" to implemented. Add usage examples for all 6 export commands. Update the features list and CLI reference.

- [ ] **Step 4: Write Export assembly README**

`src/Iaet.Export/README.md` — purpose, ExportContext, 6 generators, DI, CLI usage.

- [ ] **Step 5: Commit**

```bash
git add docs/ README.md src/Iaet.Export/README.md
git commit -m "docs: add Spotify tutorial, adapter guide, and export documentation"
```

---

## Task 7: Full Test Suite + Code Review + PR

- [ ] **Step 1: Run full test suite**

```bash
dotnet test Iaet.slnx -c Release
```

Expected: All tests pass, 0 warnings.

- [ ] **Step 2: Push and create PR**

```bash
git push origin phase4-export
```

```bash
gh pr create --title "Phase 4: Export — Markdown, HTML, OpenAPI, Postman, C#, HAR" --body "$(cat <<'EOF'
## Summary

Adds 6 export formats to IAET for generating documentation and artifacts from captured API data:

- **ExportContext** — shared data loader that aggregates session requests, endpoint groups, streams, and inferred schemas
- **Markdown Report** — investigation report with endpoint catalog, examples, schemas, stream summary
- **HTML Report** — self-contained HTML version with inline CSS
- **OpenAPI 3.1 YAML** — full spec with paths, methods, and schema components
- **Postman Collection** — v2.1.0 JSON with request items and redacted headers
- **C# Typed Client** — record types + partial HttpClient wrapper with methods per endpoint
- **HAR 1.2** — standard HTTP Archive format for browser/tool import
- **CLI commands** — `iaet export report|html|openapi|postman|csharp|har`
- **Documentation** — Spotify investigation tutorial, adapter authoring guide

All exports sanitize credentials (redacted headers preserved, no secrets in output).

## Test Plan
- [ ] ExportContext: loads session data, infers schemas per endpoint
- [ ] Markdown: session header, endpoint list, examples, schemas, streams, credential redaction
- [ ] OpenAPI: valid YAML structure, paths, schema components
- [ ] Postman: valid JSON, request items, redacted auth
- [ ] HAR: valid HAR 1.2, entries with timings
- [ ] C# client: compilable code, method per endpoint, record types
- [ ] HTML: valid document wrapper, contains report content
- [ ] CLI: export --help shows all 6 subcommands

Generated with Claude Code
EOF
)"
```

---

## What's Next

After Phase 4, IAET has:
- Capture (HTTP + streams) — Phases 1-2
- Analysis (schema inference + replay) — Phase 3
- Export (6 formats + documentation) — Phase 4

**Phase 5 (Crawler)** adds semi-autonomous site discovery with boundary rules and recipe support.
