# Iaet.Export

Generates documentation and API artifacts from captured IAET session data.

---

## Purpose

`Iaet.Export` aggregates HTTP requests, endpoint groups, WebSocket/SSE streams, and
inferred JSON schemas from a capture session into an `ExportContext`, then renders
that context to one of six output formats. All exports redact credential headers
(`Authorization`, `Cookie`, `Set-Cookie`, CSRF tokens).

---

## ExportContext

`ExportContext` is the shared data loader. It resolves all data for a session in a
single `LoadAsync` call:

```csharp
var ctx = await ExportContext.LoadAsync(
    sessionId,
    catalog,        // IEndpointCatalog
    streamCatalog,  // IStreamCatalog
    schemaInferrer  // ISchemaInferrer
);
```

| Property | Type | Description |
|---|---|---|
| `Session` | `CaptureSessionInfo` | Session metadata (name, target, date, request count) |
| `Requests` | `IReadOnlyList<CapturedRequest>` | All HTTP requests captured in the session |
| `EndpointGroups` | `IReadOnlyList<EndpointGroup>` | Deduplicated, normalized endpoint groups |
| `Streams` | `IReadOnlyList<CapturedStream>` | WebSocket, SSE, and other stream connections |
| `SchemasByEndpoint` | `IReadOnlyDictionary<string, SchemaResult>` | Inferred JSON Schema keyed by normalized endpoint |

---

## Generators

All generators are static classes with a single `Generate(ExportContext ctx)` method
that returns a string.

| Generator | Output | Format |
|---|---|---|
| `MarkdownReportGenerator` | Investigation report | Markdown |
| `HtmlReportGenerator` | Self-contained report | HTML with inline CSS |
| `OpenApiGenerator` | API specification | OpenAPI 3.1 YAML |
| `PostmanGenerator` | Request collection | Postman v2.1.0 JSON |
| `CSharpClientGenerator` | Typed HTTP client | C# source |
| `HarGenerator` | HTTP archive | HAR 1.2 JSON |

---

## DI Registration

```csharp
services.AddIaetExport();
```

`AddIaetExport` is currently a no-op registration hook — the generators are static
and have no DI dependencies. The method exists so that future service registrations
(e.g. pluggable template providers) can be added without breaking callers.

---

## CLI Usage

Each format is exposed as a subcommand of `iaet export`. The `--output` flag accepts
a file path or `-` (default) to write to stdout.

```bash
# Markdown report to stdout
iaet export report --session-id <guid>

# HTML report to file
iaet export html --session-id <guid> --output report.html

# OpenAPI 3.1 YAML
iaet export openapi --session-id <guid> --output api.yaml

# Postman Collection v2.1.0
iaet export postman --session-id <guid> --output collection.json

# Typed C# HTTP client
iaet export csharp --session-id <guid> --output ApiClient.cs

# HAR 1.2 HTTP archive
iaet export har --session-id <guid> --output session.har
```
