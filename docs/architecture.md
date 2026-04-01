# IAET Architecture

## Assembly Dependency Diagram

```mermaid
graph TD
    Core["Iaet.Core
(contracts + domain models)
No external dependencies"]
    Capture["Iaet.Capture
Playwright + CDP"]
    Catalog["Iaet.Catalog
EF Core + SQLite"]
    Schema["Iaet.Schema
JSON/C#/OpenAPI generators
+ protojson detection
+ DeepFieldResolver"]
    Replay["Iaet.Replay
HttpReplayEngine · JsonDiffer"]
    Export["Iaet.Export
9 export formats"]
    Crawler["Iaet.Crawler
BFS + RecipeRunner"]
    Explorer["Iaet.Explorer
Razor Pages + Minimal API
+ dynamic dashboard SPA"]
    Android["Iaet.Android
jadx decompilation
URL/auth/manifest extraction
BLE service discovery
L2CAP parser · HCI import
data flow tracing (Cronet)"]
    JsAnalysis["Iaet.JsAnalysis
Bundle analysis · URL extraction
CrossEndpointCorrelator
RecursiveProtojsonAnalyzer"]
    ProtocolAnalysis["Iaet.ProtocolAnalysis
WebSocket · SDP · SIP
WebRTC session reconstruction
HLS/DASH · state machines"]
    Agents["Iaet.Agents
Lead Investigator protocol
11 specialist agents"]
    Projects["Iaet.Projects
.iaet-projects/ store
auto-status detection"]
    Secrets["Iaet.Secrets
.env.iaet per project"]
    Cookies["Iaet.Cookies
Lifecycle + rotation"]
    Diagrams["Iaet.Diagrams
Sequence · DataFlow · StateMachine
DependencyGraph · ConfidenceAnnotator"]
    Cli["Iaet.Cli (dotnet global tool)"]

    Core --> Capture
    Core --> Catalog
    Core --> Schema
    Core --> Replay
    Core --> Export
    Core --> Crawler
    Core --> Android
    Core --> JsAnalysis
    Core --> ProtocolAnalysis
    Core --> Diagrams
    Projects --> Cli
    Secrets --> Cli
    Agents --> Cli
    Cookies --> Cli
    Catalog --> Export
    Schema --> Export
    Catalog --> Explorer
    Schema --> Explorer
    Replay --> Explorer
    Export --> Explorer
    JsAnalysis --> Cli
    ProtocolAnalysis --> Cli
    Android --> Cli
    Diagrams --> Cli
    Capture --> Cli
    Catalog --> Cli
    Schema --> Cli
    Replay --> Cli
    Export --> Cli
    Crawler --> Cli
    Explorer --> Cli
```

## Data Flow

```mermaid
graph TD
    Browser["Browser interaction
(extension or Playwright)"]
    Cdp["CdpNetworkListener
(Playwright CDP events)
raw IRequest / IResponse pairs"]
    Extension["Browser Extension
fetch/XHR/WebSocket/WebRTC/SSE
capture → .iaet.json"]
    Sanitizer["RequestSanitizer.SanitizeHeaders
credential headers → REDACTED"]
    CapturedReq["CapturedRequest
(immutable record)"]
    Catalog["IEndpointCatalog.SaveRequestAsync"]
    Normalizer["EndpointNormalizer.Normalize
path segment ID detection → placeholder
produces EndpointSignature"]
    InsertReq["INSERT CapturedRequestEntity"]
    UpsertGroup["UPSERT EndpointGroupEntity
(dedup + count)"]
    SQLite["SQLite
catalog.db"]
    Schema["Iaet.Schema → SchemaResult
protojson detection
DeepFieldResolver"]
    JsAnalysis["Iaet.JsAnalysis
CrossEndpointCorrelator
value tracing across endpoints"]
    Protocol["Iaet.ProtocolAnalysis
SipAnalyzer · WebRtcSessionReconstructor
WebSocketAnalyzer"]
    Replay["Iaet.Replay → ReplayResult"]
    Export["Iaet.Export → OpenAPI / Postman / HAR
narrative / smart-client-prompt"]
    ApkAnalysis["Iaet.Android
APK decompile → static extract
BLE discovery · L2CAP · HCI import
data flow tracing (Cronet)"]
    Knowledge["knowledge/*.json
endpoints · protocols · BLE profile
correlations · dependencies"]
    Dashboard["Dashboard
static HTML or dynamic SPA
(iaet dashboard / iaet explore)"]
    Diagrams["Iaet.Diagrams
sequence · data flow · state machine"]

    Browser --> Cdp
    Browser --> Extension
    Extension -->|import| Sanitizer
    Cdp --> Sanitizer
    Sanitizer --> CapturedReq
    CapturedReq --> Catalog
    Catalog --> Normalizer
    Catalog --> InsertReq
    Catalog --> UpsertGroup
    UpsertGroup --> SQLite
    SQLite --> Schema
    SQLite --> JsAnalysis
    SQLite --> Protocol
    SQLite --> Replay
    SQLite --> Export
    ApkAnalysis --> Knowledge
    JsAnalysis --> Knowledge
    Protocol --> Knowledge
    Knowledge --> Export
    Knowledge --> Dashboard
    Export --> Dashboard
    Diagrams --> Dashboard
```

## Design Decisions

### Dependency inversion via Iaet.Core
All concrete assemblies depend on `Iaet.Core`, never on each other. This keeps `Iaet.Capture` and `Iaet.Catalog` independently testable and swappable. The CLI is the only composition root.

### EF Core over raw SQLite
EF Core was chosen for migration management and LINQ query composition. The tradeoff is a larger binary; the benefit is that schema evolution (adding columns, indexes) is tracked in source-controlled migration files with zero manual SQL.

### Deduplication at write time
`EndpointGroup` rows are upserted every time a request is saved rather than computed on read. This keeps `GetEndpointGroupsAsync` O(1) per group and avoids full-table scans on large sessions.

### System.CommandLine (preview)
The CLI uses the `System.CommandLine` library for structured argument parsing, typed options, and built-in `--help` generation. The `System.CommandLine.Hosting` integration is not used; instead the DI host is built once in `Program.cs` and passed into each command factory, which keeps the host lifetime aligned with the process rather than the command.

### Header sanitization as a hard invariant
`RequestSanitizer` is not configurable. Any future opt-in mechanism would require an explicit allowlist, not a blocklist, to prevent accidental credential exposure in shared capture databases.

### Playwright for capture
Playwright (Chromium via CDP) was chosen over raw proxy interception because it can capture traffic from single-page applications that make XHR/fetch calls inside complex authentication flows without requiring SSL certificate trust overrides on the host machine.

### Deep Field Resolver for protojson
Google APIs return protojson (positional JSON arrays with no field names). The Deep Field Resolver combines three strategies to infer field names: value-type heuristics, cross-endpoint correlation (shared values across responses), and ProtoFieldMapper (matching against `.proto` field names extracted from decompiled APK source).

### L2CAP parsing for non-GATT BLE
Many BLE devices use L2CAP dynamic channels instead of GATT for high-throughput data transfer. The L2CAP protocol extractor parses connection-oriented channel frames from HCI snoop logs, enabling analysis of devices that do not expose standard GATT services.

### Dynamic dashboard SPA
The `iaet explore` command serves a thin SPA that reads project data live from the catalog database and `.iaet-projects/` directory, replacing the previous 430KB+ static HTML dashboard with an interactive experience that supports project selection, live refresh, and embedded Swagger UI.
