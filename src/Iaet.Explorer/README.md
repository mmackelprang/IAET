# Iaet.Explorer

**Phase 6 — Local Swagger-like Web UI**

`Iaet.Explorer` hosts a local ASP.NET Core web application that reads from the IAET SQLite catalog and lets you browse, inspect, and interact with captured API data directly in a browser — no external tooling required.

## Features

- **Sessions list** — browse all captured sessions with metadata
- **Endpoint browser** — table of discovered endpoints with method, normalized path, and observation count
- **Request viewer** — expandable request/response detail cards with headers and bodies
- **Schema viewer** — inferred JSON Schema, C# record, and OpenAPI fragment tabs
- **Replay** — one-click replay of any captured request with live diff results
- **Export downloads** — download session data as Markdown report, HTML, OpenAPI YAML, Postman collection, C# client, or HAR
- **Streams viewer** — WebSocket, SSE, and other captured data stream details with frame history

## Usage

```bash
# Launch with the CLI (default port 9200)
iaet explore --db catalog.db

# Use a custom port
iaet explore --db catalog.db --port 8080
```

Then open `http://localhost:9200` in your browser.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/sessions` | List all sessions |
| GET | `/api/sessions/{id}` | Get a single session |
| GET | `/api/sessions/{id}/endpoints` | List endpoint groups |
| GET | `/api/sessions/{id}/endpoints/{sig}/requests` | Requests for an endpoint |
| GET | `/api/sessions/{id}/endpoints/{sig}/schema` | Inferred schema |
| GET | `/api/sessions/{id}/streams` | Streams for a session |
| POST | `/api/replay/{requestId}` | Replay a captured request |
| GET | `/api/sessions/{id}/export/{format}` | Export (report/html/openapi/postman/csharp/har) |

## Programmatic Use

```csharp
var app = ExplorerApp.Build("catalog.db", port: 9200);
await app.RunAsync();
```

## Architecture

The Explorer is an ASP.NET Core library (`OutputType=Library`) with:

- `ExplorerApp.cs` — static `Build(dbPath, port)` factory
- `Api/` — Minimal API endpoint registrations (SessionsApi, EndpointsApi, StreamsApi, SchemaApi, ReplayApi, ExportApi)
- `Pages/` — Razor Pages UI (Index, Session, Endpoint, Streams)
- `wwwroot/css/site.css` — minimal CSS
