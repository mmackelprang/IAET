# IAET User Guide

IAET (Internal API Extraction Toolkit) discovers, captures, analyzes, and documents undocumented APIs from web applications and Android APKs. This guide covers every supported workflow from installation through final export.

---

## Getting Started

### Installation

```bash
dotnet tool install -g iaet
```

Requires .NET 9 SDK or later. Verify the install:

```bash
iaet --version
```

For APK analysis, install **jadx** separately and ensure it is on your `PATH`:
- Download from https://github.com/skylot/jadx/releases
- Or install via package manager: `brew install jadx` / `scoop install jadx`

For Playwright-based capture (optional — not needed if you use the browser extension):

```bash
npx playwright install chromium
```

### Creating Your First Project

Every investigation is organized as a project. Projects store configuration, captures, knowledge, round plans, agent findings, and final outputs in a single directory under `.iaet-projects/`.

```bash
iaet project create \
  --name my-target \
  --url https://example.com \
  --auth-required \
  --display-name "My Target App"
```

Options:

| Flag | Default | Description |
|------|---------|-------------|
| `--name` | required | Slug used as directory name and CLI identifier |
| `--url` | required | Starting URL of the target |
| `--target-type` | `web` | `web`, `android`, or `desktop` |
| `--auth-required` | false | Marks the target as needing authentication |
| `--display-name` | same as name | Human-readable label shown in the dashboard |

List existing projects:

```bash
iaet project list
iaet project status --name my-target
```

---

## Use Case 1: Investigating a Web Application

This workflow uses the browser extension to capture all HTTP traffic, WebSocket frames, WebRTC signaling, and SSE events from Chrome — no proxy, no certificate installation.

### Step 1: Create a Project

```bash
iaet project create --name my-target --url https://example.com --auth-required
```

### Step 2: Install the Browser Extension

1. Open Chrome and navigate to `chrome://extensions`
2. Enable **Developer mode** (toggle in the top-right)
3. Click **Load unpacked**
4. Select the `extensions/iaet-capture/dist` directory from this repository

The IAET Capture icon appears in the toolbar. Click it to open the popup.

**What the extension captures:**

| Signal | Details |
|--------|---------|
| HTTP (fetch/XHR) | Full request/response headers and bodies, timing |
| WebSocket | All frames (text and binary) with direction and timestamps |
| WebRTC | SDP offers/answers, ICE candidates, connection state |
| SSE | Event stream with event types and data payloads |

The extension captures up to 10,000 requests per session and up to 1,000 WebSocket frames per connection.

### Step 3: Capture Traffic

1. Click the IAET icon in Chrome
2. Enter a **session name** (e.g., `homepage-2026-03-31`) and the **target application** name
3. Click **Start Recording**
4. Browse the target application normally — log in, navigate features, trigger API calls
5. When done, click **Stop Recording**
6. Click **Export** to download the `.iaet.json` file

The exported file contains the full session including all requests, streams, and frame data.

### Step 4: Import the Capture

```bash
# Import into the SQLite catalog
iaet import --file capture.iaet.json

# Import AND archive a compressed copy under the project
iaet import --file capture.iaet.json --project my-target
```

The `--project` flag copies a `.gz`-compressed version of the capture into `.iaet-projects/my-target/captures/` for long-term storage, in addition to importing into the catalog.

**Automated import via HTTP listener** (useful for one-click export from the extension):

```bash
iaet import --listen --port 7474
```

The extension's **Send to IAET** button posts captures directly to this listener.

### Step 5: Analyze and Generate Reports

**View what was captured:**

```bash
# List sessions
iaet catalog sessions

# List endpoints for a session
iaet catalog endpoints --session-id <guid>

# List streams (WebSocket, SSE, WebRTC, etc.)
iaet streams list --session-id <guid>
iaet streams show --stream-id <guid>
iaet streams frames --stream-id <guid>
```

**Infer schemas from response bodies:**

```bash
# Show JSON Schema, C# record, and OpenAPI fragment for an endpoint
iaet schema infer --session-id <guid> --endpoint "GET /api/users"

# Show a specific format only
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format json
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format csharp
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format openapi
```

Note: If responses use **protojson** format (positional JSON arrays — common in Google APIs), IAET detects this and warns that field names are inferred from structure rather than known.

**Replay requests to test for changes:**

```bash
# Replay a single request with diff output
iaet replay run --request-id <guid>

# Dry run — show what would be replayed without sending HTTP
iaet replay run --request-id <guid> --dry-run

# Replay one representative request per endpoint
iaet replay batch --session-id <guid>
```

**Export session data:**

```bash
# OpenAPI 3.1 YAML
iaet export openapi --session-id <guid> --output api.yaml

# Investigation narrative (detailed Markdown writeup)
iaet export narrative --session-id <guid> --output narrative.md

# AI client-generation prompt (for feeding to an LLM to write a typed client)
iaet export client-prompt --session-id <guid> --output prompt.md

# Self-contained HTML report
iaet export html --session-id <guid> --output report.html

# Plain Markdown report
iaet export report --session-id <guid> --output report.md

# Postman Collection v2.1.0
iaet export postman --session-id <guid> --output collection.json

# Typed C# HTTP client
iaet export csharp --session-id <guid> --output ApiClient.cs

# HAR 1.2 archive
iaet export har --session-id <guid> --output session.har
```

All exports pass through credential redaction — `Authorization`, `Cookie`, `Set-Cookie`, and CSRF token values are replaced with `<REDACTED>`.

### Step 6: View the Dashboard

```bash
# All projects overview
iaet dashboard

# Single project
iaet dashboard --project my-target

# Skip auto-open
iaet dashboard --project my-target --open=false
```

The dashboard renders a self-contained HTML file at `.iaet-projects/dashboard.html` (all projects) or `.iaet-projects/my-target/output/dashboard.html` (single project). It includes:

- Project status and metadata
- API endpoint table with methods, paths, and observation counts
- Stream summary (WebSocket, SSE, WebRTC)
- Inline Swagger UI for any generated OpenAPI specs
- **Next Steps** tab with recommendations based on what has and has not been captured

Alternative: run the Python script directly:

```bash
python3 scripts/generate-dashboard.py                         # all projects
python3 scripts/generate-dashboard.py .iaet-projects/my-target  # single project
```

### Step 7: Cookie Analysis (Optional)

If the target uses cookies for session management, IAET tracks them across snapshots:

```bash
# List cookie snapshots (snapshots are saved by the Cookie & Session agent or manually)
iaet cookies snapshot --project my-target

# Compare two snapshots to see what changed
iaet cookies diff --project my-target --before <guid> --after <guid>

# Lifecycle analysis — expiry warnings, rotation detection
iaet cookies analyze --project my-target
```

---

## Use Case 2: Investigating an Android APK

IAET uses **jadx** to decompile APK files and then statically extracts API endpoints, authentication patterns, declared permissions, and network security configuration.

### Step 1: Create a Project

```bash
iaet project create \
  --name my-app \
  --url https://api.example.com \
  --target-type android
```

Use `--url` for the base API URL even if it is not yet confirmed — you will refine it from the APK analysis.

### Step 2: Decompile the APK

```bash
iaet apk decompile \
  --project my-app \
  --apk path/to/app.apk
```

With optional flags:

```bash
# Specify a non-default jadx location
iaet apk decompile --project my-app --apk app.apk --jadx-path /opt/jadx/bin/jadx

# Provide a ProGuard mapping file to restore obfuscated names
iaet apk decompile --project my-app --apk app.apk --mapping mapping.txt
```

The APK is copied to `.iaet-projects/my-app/apk/app.apk` and decompiled Java source is written to `.iaet-projects/my-app/apk/decompiled/`.

If jadx fails (native-only APK, heavy obfuscation), fall back to apktool for resources:

```bash
apktool d app.apk -o .iaet-projects/my-app/apk/resources/
```

The `analyze` command will pick up `AndroidManifest.xml` and `res/xml/network_security_config.xml` from that path.

### Step 3: Run Static Analysis

```bash
iaet apk analyze --project my-app
```

This extracts:

- **API endpoints** — Retrofit annotations (`@GET`, `@POST`, etc.), OkHttp builder patterns, and URL string literals
- **Auth patterns** — API key constants, Google API keys (`AIza...`), `addHeader("Authorization", ...)` patterns
- **Manifest info** — package name, version, minSdk, targetSdk, declared permissions
- **Exported components** — services, receivers, and providers accessible to other apps
- **Network security config** — cleartext traffic policy, certificate pinning domains

### Step 4: Review Extracted Findings

Analysis writes three files to `.iaet-projects/my-app/knowledge/`:

**`endpoints.json`** — API surface extracted from source:
```json
{
  "endpoints": [
    {
      "signature": "GET users/{id}",
      "confidence": "high",
      "source": "ApiService.java",
      "context": "@GET(\"users/{id}\")"
    },
    {
      "signature": "https://api.example.com/v2",
      "confidence": "high",
      "source": "ApiClient.java",
      "context": "BASE_URL constant"
    }
  ]
}
```

**`permissions.json`** — Manifest metadata and permissions:
```json
{
  "packageName": "com.example.app",
  "versionName": "2.3.1",
  "minSdk": 24,
  "targetSdk": 34,
  "permissions": [
    "android.permission.INTERNET",
    "android.permission.RECORD_AUDIO",
    "android.permission.BLUETOOTH_CONNECT"
  ],
  "exportedServices": ["com.example.SipService"],
  "exportedReceivers": ["com.example.BootReceiver"]
}
```

**`network-security.json`** — TLS policy and certificate pinning:
```json
{
  "cleartextDefault": false,
  "cleartextDomains": ["debug.example.com"],
  "pinnedDomains": [
    { "Domain": "api.example.com", "Pins": ["sha256/..."] }
  ]
}
```

Note: **Certificate-pinned domains will block standard `iaet capture`**. MITM capture for pinned domains requires either a patched APK or a rooted device.

### Step 5: View the Dashboard

```bash
iaet dashboard --project my-app
```

The dashboard shows extracted endpoints, auth patterns, permissions, and network security findings alongside any subsequent network capture sessions.

### Step 6: Combine with Network Capture (Optional)

For a complete picture, pair static analysis with live traffic capture using the browser extension or Playwright on the device's network (through a proxy).

After capturing runtime traffic, import it:

```bash
iaet import --file runtime-capture.iaet.json --project my-app
```

The dashboard and exports will then include both statically extracted and dynamically observed endpoints. APK-only endpoints (present in `knowledge/endpoints.json` but not in the catalog) are flagged as "unobserved — needs runtime capture."

---

## Use Case 3: Agent-Driven Investigation

IAET includes a team of specialist Claude Code sub-agents coordinated by a **Lead Investigator** agent. The system performs multi-round investigations autonomously, asking the human only for actions that require browser interaction or credential handling.

### The Agent Team

| Agent | File | Role |
|-------|------|------|
| Lead Investigator | `agents/lead-investigator.md` | Orchestrator — plans rounds, dispatches specialists, merges findings |
| Network Capture | `agents/network-capture.md` | HTTP and stream traffic capture via Playwright |
| Cookie & Session | `agents/cookie-session.md` | Cookie enumeration, lifecycle analysis, storage scanning |
| Crawler | `agents/crawler.md` | BFS page traversal, element discovery |
| JS Analyzer | `agents/js-analyzer.md` | Static JS bundle analysis for URL and API extraction |
| Protocol Analyzer | `agents/protocol-analyzer.md` | WebSocket, SDP, and HLS stream analysis |
| Schema Analyzer | `agents/schema-analyzer.md` | Dependency graphs, auth chains, rate limit detection |
| APK Analyzer | `agents/apk-analyzer.md` | Android decompilation and static extraction |
| Diagram Generator | `agents/diagram-generator.md` | Mermaid sequence, flow, and state diagrams |
| Report Assembler | `agents/report-assembler.md` | Final export — OpenAPI, Postman, narrative, coverage |

### Starting an Investigation with the Lead Investigator

**Step 1:** Create the project (if not already done):

```bash
iaet project create --name my-target --url https://example.com --auth-required
```

**Step 2:** Start the investigation context:

```bash
iaet investigate --project my-target
```

**Step 3:** In Claude Code, tell the Lead Investigator to begin:

```
Investigate the project my-target following the Lead Investigator protocol in agents/lead-investigator.md
```

The Lead Investigator will:
1. Load the project state and any existing knowledge
2. Assess the target (SPA vs multi-page, auth requirements, likely protocols)
3. If auth is required, ask you to log in and capture your session cookies
4. Plan the first round and dispatch specialists in parallel

### How Rounds Work

Each round follows this sequence:

1. **Lead writes a round plan** to `.iaet-projects/my-target/rounds/{NNN}-round/plan.json`:
   ```json
   {
     "roundNumber": 1,
     "rationale": "Initial discovery — capture traffic, enumerate cookies, crawl pages",
     "dispatches": [
       { "agent": "network-capture", "targets": ["https://example.com"] },
       { "agent": "cookie-session", "targets": ["https://example.com"] },
       { "agent": "crawler",         "targets": ["https://example.com"] }
     ],
     "humanActions": []
   }
   ```

2. **Capture-stage agents run in parallel** — network capture, cookie enumeration, crawler

3. **Analysis-stage agents run in parallel** — JS analyzer, protocol analyzer, schema analyzer

4. **Lead merges findings** into `knowledge/`:
   - `endpoints.json` — all discovered API endpoints with confidence levels
   - `cookies.json` — auth-critical cookies, rotation patterns
   - `protocols.json` — WebSocket URLs, subprotocols, message type inventories
   - `dependencies.json` — auth chains, endpoint call ordering

5. **Lead decides**: another round (if new endpoints found) or finalize

Check round status at any time:

```bash
iaet round status --project my-target
```

### Human-in-the-Loop Actions

The Lead Investigator will pause and ask you when:

- **Auth is required** — prompts you to log in via browser, then dispatches Cookie & Session to capture the resulting cookies
- **Cookies are expiring** — warns when auth cookies have less than 10 minutes remaining and asks you to re-authenticate
- **Coverage decision** — once no new endpoints are being found, presents a summary and asks whether to continue or finalize

Store captured credentials for agent use:

```bash
iaet secrets set --project my-target --key SESSION_COOKIE --value "..."
iaet secrets set --project my-target --key CSRF_TOKEN --value "..."
```

Secrets are stored in `.iaet-projects/my-target/.env.iaet`, which is gitignored and never included in exports.

### Finalization

When you tell the Lead Investigator to finalize (or when coverage thresholds are met), it dispatches:

1. **Diagram Generator** — produces Mermaid sequence diagrams, flow diagrams, and state machines in `output/`
2. **Report Assembler** — generates all export formats (OpenAPI, narrative, Postman, HTML report) in `output/`

Then generate the dashboard:

```bash
iaet dashboard --project my-target
```

---

## Browser Extension

### Installing

1. Navigate to `chrome://extensions` in Chrome
2. Enable **Developer mode**
3. Click **Load unpacked**
4. Select `extensions/iaet-capture/dist`

The extension is built with Vite and TypeScript. To rebuild after making changes:

```bash
cd extensions/iaet-capture
npm install
npm run build
```

### Capturing Traffic

The extension intercepts traffic at the page level using injected scripts and a Manifest V3 service worker — no proxy or certificate installation required.

**Captured signal types:**

| Type | Implementation | Notes |
|------|---------------|-------|
| HTTP fetch | `window.fetch` wrapper in inject.ts | Full headers and body |
| HTTP XHR | `XMLHttpRequest` wrapper in inject.ts | Full headers and body |
| WebSocket | `WebSocket` wrapper in inject.ts | Text and binary frames; binary encoded as base64 |
| WebRTC | `RTCPeerConnection` wrapper in inject.ts | SDP offers/answers, ICE candidates |
| SSE | `EventSource` wrapper in inject.ts | Event type and data per event |

**Limits:**
- Maximum 10,000 requests per session
- Maximum 1,000 WebSocket frames per connection

### Starting and Stopping

Click the extension icon to open the popup:
- Enter a **session name** and **target application** name
- Click **Start Recording** — the badge turns red with a count
- Click **Stop Recording** when done

### Exporting Captures

**Export as file:**
- Click **Export** in the popup
- Saves a `.iaet.json` file to your downloads folder

**Send directly to IAET:**
- Start the import listener: `iaet import --listen --port 7474`
- Click **Send to IAET** in the popup
- The capture is imported immediately into the catalog

### The .iaet.json Format

The exported file is a JSON document with schema version `"1.0"`. Key fields:

```json
{
  "iaetVersion": "1.0",
  "exportedAt": "2026-03-31T12:00:00.000Z",
  "session": {
    "id": "<uuid>",
    "name": "my-session",
    "targetApplication": "Example App",
    "startedAt": "...",
    "stoppedAt": "..."
  },
  "requests": [ /* IaetRequest[] */ ],
  "streams":  [ /* IaetStream[] */ ]
}
```

See `docs/capture-format.md` for the complete schema specification.

---

## Dashboard

### Generating the Dashboard

```bash
# All projects — writes .iaet-projects/dashboard.html
iaet dashboard

# Single project — writes .iaet-projects/<name>/output/dashboard.html
iaet dashboard --project my-target

# Generate without auto-opening in browser
iaet dashboard --project my-target --open=false
```

Alternative using the Python script directly:

```bash
python3 scripts/generate-dashboard.py
python3 scripts/generate-dashboard.py .iaet-projects/my-target
```

### Multi-Project View

The root dashboard (`iaet dashboard` with no `--project` flag) scans all directories under `.iaet-projects/` and renders a card for each. Each card shows:

- Project name, target URL, and target type (web / android)
- Current investigation status and round number
- Quick summary of discovered endpoints and streams

### Swagger UI for OpenAPI Specs

If a project has a generated `output/api.yaml` or `output/openapi.yaml`, the dashboard embeds a full Swagger UI for it, allowing you to browse and test endpoints interactively.

Generate the OpenAPI spec first:

```bash
iaet export openapi --session-id <guid> --output .iaet-projects/my-target/output/api.yaml
```

### Next Steps Tab

The dashboard analyzes what has been captured and generates prioritized recommendations — for example:
- "No stream captures found — enable stream monitoring in the browser extension"
- "5 endpoints have no schema inferred — run `iaet schema infer`"
- "Cookie analysis not run — run `iaet cookies analyze`"

---

## CLI Reference

Complete listing of all commands with their options.

### `iaet project`

```bash
iaet project create  --name <slug> --url <url>
                     [--target-type web|android|desktop]
                     [--auth-required]
                     [--display-name <name>]

iaet project list

iaet project status  --name <name>

iaet project archive --name <name>
```

### `iaet capture`

```bash
iaet capture start   --target <name> --url <url> --session <name>
                     [--profile <browser-profile-name>]
                     [--headless]
                     [--capture-streams]          # default: enabled
                     [--capture-samples]          # capture raw frame payloads
                     [--capture-duration <sec>]
                     [--capture-frames <n>]       # max frames per stream (default: 1000)

iaet capture run     --recipe <path.ts> --session <name>
```

### `iaet import`

```bash
iaet import --file <path.iaet.json>   [--project <name>]
iaet import --listen                  [--port <n>]    # default: 7474
```

The `--project` flag saves a compressed (`.iaet.json.gz`) archive copy in the project's `captures/` directory in addition to importing into the catalog.

### `iaet catalog`

```bash
iaet catalog sessions
iaet catalog endpoints  --session-id <guid>
```

### `iaet streams`

```bash
iaet streams list    --session-id <guid>
iaet streams show    --stream-id <guid>
iaet streams frames  --stream-id <guid>    # requires --capture-samples during capture
```

### `iaet schema`

```bash
iaet schema infer  --session-id <guid> --endpoint "GET /api/path"
iaet schema show   --session-id <guid> --endpoint "GET /api/path"
                   --format <json|csharp|openapi>
```

IAET automatically detects **protojson** responses (root-level JSON arrays used by Google APIs) and annotates them with a warning that field names are inferred from positional structure.

### `iaet replay`

```bash
iaet replay run    --request-id <guid>  [--dry-run]
iaet replay batch  --session-id <guid>  [--dry-run]
```

Rate limits: 10 requests/minute, 100 requests/day. Uses Polly for retry and circuit breaking.

### `iaet export`

All export subcommands accept `--session-id <guid>` and `--output <path>` (defaults to stdout if omitted):

```bash
iaet export report         # Markdown investigation report
iaet export html           # Self-contained HTML report
iaet export openapi        # OpenAPI 3.1 YAML specification
iaet export postman        # Postman Collection v2.1.0
iaet export csharp         # Typed C# HTTP client
iaet export har            # HAR 1.2 HTTP archive
iaet export narrative      # Detailed investigation narrative
iaet export client-prompt  # AI client-generation prompt
```

### `iaet crawl`

```bash
iaet crawl --url <url>
           [--target <name>]
           [--session <name>]
           [--max-depth <n>]
           [--max-pages <n>]
           [--max-duration <seconds>]
           [--headless]
           [--blacklist <pattern>]...     # e.g. "/logout" "/admin/*"
           [--exclude-selector <css>]...  # skip elements matching selector
           [--output <path>]             # write crawl report JSON
```

### `iaet apk`

```bash
iaet apk decompile --project <name> --apk <path>
                   [--jadx-path <path>]   # default: "jadx" on PATH
                   [--mapping <path>]     # ProGuard mapping.txt

iaet apk analyze   --project <name>
```

Writes `endpoints.json`, `permissions.json`, and `network-security.json` to `.iaet-projects/<name>/knowledge/`.

### `iaet cookies`

```bash
iaet cookies snapshot --project <name>               # list snapshots
iaet cookies diff     --project <name>
                      --before <guid>
                      --after <guid>
iaet cookies analyze  --project <name>               # lifecycle + rotation analysis
```

### `iaet secrets`

```bash
iaet secrets set   --project <name> --key <key> --value <value>
iaet secrets get   --project <name> --key <key>
iaet secrets list  --project <name>                  # shows keys only, values hidden
iaet secrets audit --project <name>
```

Secrets are stored in `.iaet-projects/<name>/.env.iaet`. This file is added to `.gitignore` automatically on first project creation.

### `iaet round`

```bash
iaet round status  --project <name>
```

Shows the current round number, plan rationale, dispatch count, and received findings count.

### `iaet dashboard`

```bash
iaet dashboard                         # all projects
iaet dashboard --project <name>        # single project
iaet dashboard --project <name> --open=false   # skip auto-open
```

### `iaet explore`

```bash
iaet explore --db catalog.db           # default port: 9200
iaet explore --db catalog.db --port 8080
```

Opens a local Swagger-like UI at `http://localhost:9200` for browsing sessions, endpoints, schemas, streams, and replaying requests.

### `iaet investigate`

```bash
iaet investigate                       # guided interactive wizard (no project)
iaet investigate --project <name>      # agent-based workflow for a specific project
```

Without `--project`: interactive menu-driven wizard (capture → analyze → export). With `--project`: sets up context for the Lead Investigator agent team.

---

## Project Structure

### `.iaet-projects/` Layout

```
.iaet-projects/
  dashboard.html               ← root dashboard (all projects)
  my-target/
    project.json               ← project config (name, URL, target type, status)
    .env.iaet                  ← secrets (gitignored, never committed)
    captures/
      20260331-190108-capture.iaet.json.gz   ← compressed capture archives
    rounds/
      001-round/
        plan.json              ← Lead Investigator's round plan
        findings.json          ← merged specialist findings
    knowledge/
      endpoints.json           ← accumulated endpoint inventory
      cookies.json             ← cookie lifecycle summary
      protocols.json           ← stream/protocol inventory
      dependencies.json        ← auth chains and call ordering
      permissions.json         ← APK permissions (android projects)
      network-security.json    ← cert pinning config (android projects)
    output/
      narrative.md             ← investigation narrative
      report.md                ← Markdown report
      api.yaml                 ← OpenAPI 3.1 spec
      collection.json          ← Postman collection
      ApiClient.cs             ← typed C# client
      diagrams/                ← Mermaid diagrams (PNG + source)
      dashboard.html           ← single-project dashboard
    apk/                       ← android projects only
      app.apk
      mapping.txt
      decompiled/              ← jadx Java output
      resources/               ← apktool resource output
    investigation.log          ← append-only agent activity log
```

### `project.json` Schema

```json
{
  "name": "my-target",
  "displayName": "My Target App",
  "targetType": "web",
  "entryPoints": [
    { "url": "https://example.com", "label": "Main" }
  ],
  "authRequired": true,
  "authMethod": "browser-login",
  "focusAreas": [],
  "currentRound": 3,
  "status": "investigating",
  "createdAt": "2026-03-31T...",
  "lastActivityAt": "2026-03-31T..."
}
```

### Secrets Management

Secrets are stored in `.iaet-projects/<name>/.env.iaet` as `KEY=value` pairs, one per line. IAET's `secrets` commands manage this file.

- The file is added to `.gitignore` automatically when a project is created
- Agent prompts explicitly forbid including secret values in findings or knowledge files
- All exports pass through credential redaction regardless

### Capture Archival

When you run `iaet import --project <name>`, the original `.iaet.json` file is compressed and saved as a timestamped `.gz` file under the project's `captures/` directory. This lets you re-import or audit raw captures later without bloating the SQLite catalog.

---

## Data Stream Support

IAET captures and catalogs non-HTTP data channels as `CapturedStream` objects with `StreamFrame` records:

| Protocol | Value | Browser Extension | Playwright Capture |
|----------|-------|-------------------|-------------------|
| WebSocket | `WebSocket` | Yes | Yes |
| Server-Sent Events | `ServerSentEvents` | Yes | Yes |
| WebRTC | `WebRtc` | Yes (SDP/ICE) | Yes |
| HLS media segments | `HlsStream` | No | Yes |
| MPEG-DASH segments | `DashStream` | No | Yes |
| gRPC-Web | `GrpcWeb` | No | Yes |

Add support for new wire formats by implementing `IProtocolListener` and registering it in DI:

```csharp
public interface IProtocolListener
{
    string ProtocolName { get; }
    StreamProtocol Protocol { get; }
    bool CanAttach(ICdpSession cdpSession);
    Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct);
    Task DetachAsync(CancellationToken ct);
}
```

---

## Writing Target Adapters

`IApiAdapter` lets you attach target-specific enrichment to the generic capture pipeline:

```csharp
public interface IApiAdapter
{
    bool CanHandle(CapturedRequest request);
    EndpointDescriptor Describe(CapturedRequest request);
}
```

- `CanHandle` — return `true` if this adapter recognizes the request (e.g., by host or path prefix)
- `Describe` — return an `EndpointDescriptor` enriched with operation name, parameter metadata, or auth type gleaned from domain knowledge of that target

Register adapters in DI alongside the core services. The catalog calls `Describe` when a matching adapter is present, storing the richer descriptor alongside the raw request.

---

## Coming Soon

### BLE (Bluetooth Low Energy) Analysis — Phase 2

IAET Phase 2 will include a BLE investigation module for mobile apps that use Bluetooth:

- Service and characteristic UUID enumeration
- GATT profile documentation
- Sniffed BLE packet analysis (Wireshark/btsnoop HCI log import)
- Cross-reference with Android APK BLE permissions (`BLUETOOTH_CONNECT`, `BLUETOOTH_SCAN`)

This work is currently in progress. The `Iaet.Ble` project will integrate with the APK analyzer workflow and produce a `ble-profile.json` knowledge file alongside the existing `permissions.json`.

---

## Legal & Ethical Guidelines

- **Rate limiting** — introduce deliberate delays between automated actions; never hammer an endpoint.
- **Credential handling** — IAET redacts `Authorization`, `Cookie`, `Set-Cookie`, and CSRF token headers before persisting. Do not disable sanitization.
- **Single-account research** — only use accounts you own or have explicit written permission to test.
- **No credential publishing** — never commit capture databases, session files, `.env.iaet`, or logs that contain authentication material.

Use IAET only on systems you own or have explicit permission to test. Unauthorized access to computer systems is illegal in most jurisdictions.
