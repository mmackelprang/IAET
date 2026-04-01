# IAET User Guide

IAET (Internal API Extraction Toolkit) discovers, captures, analyzes, and documents undocumented APIs from web applications, Android APKs, and BLE devices. This guide covers every supported workflow from installation through final export.

---

## Getting Started

### Installation

**From source (recommended):**

```bash
# Linux / macOS / Git Bash
bash scripts/install.sh

# Windows PowerShell
pwsh scripts/install.ps1
```

These scripts build the CLI, pack it as a NuGet package, and install it as a dotnet global tool. After installation, `iaet` is available on your PATH.

**Manual install:**

```bash
dotnet pack src/Iaet.Cli/Iaet.Cli.csproj -c Release -o artifacts/
dotnet tool install -g Iaet.Cli --add-source artifacts/ --version 0.1.0
```

Requires .NET 9 SDK or later. Verify the install:

```bash
iaet --version
```

**Prerequisites:**

| Requirement | Purpose | Install |
|-------------|---------|---------|
| **.NET 10 SDK** | Core runtime for IAET | [dot.net/download](https://dot.net/download) |
| **Python 3.10+** | Dashboard generator script | [python.org](https://python.org) |

**External tools for specific analysis types:**

| Analysis Type | Tool | Version | Purpose | Install |
|---------------|------|---------|---------|---------|
| **Web capture (Playwright)** | Chromium | Latest | Browser automation for `iaet capture` | `npx playwright install chromium` |
| **Web capture (Extension)** | Chrome | Latest | Manual capture via IAET browser extension | Load `extensions/iaet-capture/dist` in Chrome |
| **Android APK decompile** | **jadx** | 1.5+ | Decompile DEX → Java source | [github.com/skylot/jadx/releases](https://github.com/skylot/jadx/releases) or `brew install jadx` / `scoop install jadx` / `choco install jadx` |
| **Android APK resources** | **apktool** | 2.9+ | Decode AndroidManifest.xml, resources | [apktool.org](https://apktool.org) or `brew install apktool` / `choco install apktool` |
| **Android HCI log** | Android device | — | Bluetooth HCI snoop log capture | Enable in Developer Options → "Enable Bluetooth HCI snoop log" |
| **Java runtime** (for jadx) | **JDK 11+** | 11+ | Required by jadx | `brew install openjdk@11` / download from [adoptium.net](https://adoptium.net) |

> **Note:** jadx and apktool require Java 11+. If your default `java` is older, set `JAVA_HOME` before running:
> ```bash
> export JAVA_HOME="/path/to/jdk-11"
> iaet apk decompile --project my-app --apk app.apk --jadx-path /path/to/jadx
> ```

**Future analysis types (not yet implemented):**

| Analysis Type | Tools Needed | Status |
|---------------|-------------|--------|
| .NET EXE/DLL | ILSpy CLI, dnSpy | Planned (Phase 6) |
| Windows native (PE) | API Monitor, Fiddler | Planned |
| Linux native (ELF) | Ghidra, ltrace, strace | Planned |
| iOS IPA | class-dump, otool (requires Mac) | Planned |
| Electron apps | asar extract, Node.js | Planned |

This table will be updated as new analysis capabilities are added.

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

List and inspect projects:

```bash
iaet project list
iaet project status --name my-target
```

Project status is auto-detected from the presence of captures, knowledge files, and exports. You can also manually set lifecycle state:

```bash
iaet project complete --name my-target   # mark investigation as done
iaet project rerun    --name my-target   # re-enable for further investigation
iaet project archive  --name my-target   # archive (hide from dashboard)
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
iaet schema infer --session-id <guid> --endpoint "GET /api/users"
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format json
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format csharp
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format openapi
```

Note: If responses use **protojson** format (positional JSON arrays — common in Google APIs), IAET detects this automatically. The Deep Field Resolver infers field names using value-type heuristics, cross-endpoint correlation, and (for Android projects) ProtoFieldMapper matching against `.proto` field names extracted from decompiled APK source.

**Cross-endpoint correlation:**

```bash
iaet analyze correlate --project my-target --session-id <guid>
```

This traces shared values across endpoints and streams (e.g., an ID returned by one endpoint appears as a parameter in another). Results are written to `knowledge/correlations.json` and used to improve protojson field name resolution.

**Replay requests to test for changes:**

```bash
iaet replay run --request-id <guid>
iaet replay run --request-id <guid> --dry-run
iaet replay batch --session-id <guid>
```

**Export session data:**

```bash
# Export with canonical filenames to the project output directory
iaet export openapi        --session-id <guid> --project my-target
iaet export narrative      --session-id <guid> --project my-target
iaet export html           --session-id <guid> --project my-target
iaet export report         --session-id <guid> --project my-target
iaet export postman        --session-id <guid> --project my-target
iaet export csharp         --session-id <guid> --project my-target
iaet export har            --session-id <guid> --project my-target
iaet export client-prompt  --session-id <guid> --project my-target

# Or specify an output path directly
iaet export openapi --session-id <guid> --output api.yaml
```

When `--project` is used, exports are written to `.iaet-projects/<name>/output/` with canonical filenames (e.g., `api.yaml`, `narrative.md`, `report.md`, `collection.json`, `ApiClient.cs`).

**Generate an adaptive client prompt:**

```bash
iaet export smart-client-prompt --project my-target
iaet export smart-client-prompt --project my-target --language Python
```

The smart-client-prompt reads the project's knowledge directory (endpoints, protocols, BLE profiles, correlations) and produces an adaptive prompt tailored to the target. For web projects it focuses on HTTP client generation; for BLE projects it focuses on device communication; for hybrid projects it covers both. The `--language` flag controls the target language (default: C#).

### Step 6: View the Dashboard

**Dynamic dashboard (recommended):**

```bash
iaet explore --db catalog.db --projects .iaet-projects
```

This starts a local web server at `http://localhost:9200` with a thin SPA that provides:

- Project selector with live data from the catalog database and `.iaet-projects/` directory
- API endpoint table with methods, paths, and observation counts
- Stream summary (WebSocket, SSE, WebRTC)
- Embedded Swagger UI for generated OpenAPI specs
- Next-steps recommendations based on coverage gaps

Add `--port 8080` to change the listening port.

**Static dashboard (HTML file):**

```bash
iaet dashboard                         # all projects
iaet dashboard --project my-target     # single project
iaet dashboard --project my-target --open   # auto-open in browser
```

Generates a self-contained HTML file at `.iaet-projects/dashboard.html` (all projects) or `.iaet-projects/my-target/output/dashboard.html` (single project).

### Step 7: Cookie Analysis (Optional)

If the target uses cookies for session management:

```bash
iaet cookies snapshot --project my-target
iaet cookies diff     --project my-target --before <guid> --after <guid>
iaet cookies analyze  --project my-target
```

---

## Use Case 2: Investigating an Android APK

IAET uses **jadx** to decompile APK files and statically extracts API endpoints, authentication patterns, declared permissions, network security configuration, and network data flows (including Cronet HTTP stack support).

### Step 1: Create a Project

```bash
iaet project create \
  --name my-app \
  --url https://api.example.com \
  --target-type android
```

### Step 2: Decompile the APK

```bash
iaet apk decompile --project my-app --apk path/to/app.apk

# With optional flags
iaet apk decompile --project my-app --apk app.apk --jadx-path /opt/jadx/bin/jadx
iaet apk decompile --project my-app --apk app.apk --mapping mapping.txt
```

The APK is copied to `.iaet-projects/my-app/apk/app.apk` and decompiled Java source is written to `.iaet-projects/my-app/apk/decompiled/`.

### Step 3: Run Static Analysis

```bash
iaet apk analyze --project my-app
```

This extracts:

- **API endpoints** — Retrofit annotations (`@GET`, `@POST`, etc.), OkHttp builder patterns, URL string literals
- **Auth patterns** — API key constants, Google API keys (`AIza...`), `addHeader("Authorization", ...)` patterns
- **Manifest info** — package name, version, minSdk, targetSdk, declared permissions
- **Exported components** — services, receivers, and providers accessible to other apps
- **Network security config** — cleartext traffic policy, certificate pinning domains

**With network data flow tracing:**

```bash
iaet apk analyze --project my-app --trace-dataflow
```

The `--trace-dataflow` flag traces network response data through callback chains, response handlers, and data binding expressions to map which API responses drive which UI elements. This includes support for Cronet HTTP stack (used by many Google apps and apps built with Flutter/Chromium networking).

Analysis writes to `.iaet-projects/my-app/knowledge/`:
- `endpoints.json` — API surface extracted from source
- `permissions.json` — manifest metadata and permissions
- `network-security.json` — TLS policy and certificate pinning

### Step 4: ProtoFieldMapper (for protobuf/protojson APIs)

If the APK uses Protocol Buffers (common in Google apps), the `apk analyze` step also runs the **ProtoFieldMapper**, which:

1. Scans decompiled Java source for `.proto`-generated classes
2. Extracts field names, numbers, and types from the generated code
3. Maps these against protojson responses captured at runtime to replace positional array indices with meaningful field names

The mapper results feed into the schema inference pipeline automatically.

### Step 5: Combine with Network Capture (Optional)

For a complete picture, capture live traffic and correlate with static findings:

```bash
iaet import --file runtime-capture.iaet.json --project my-app
iaet analyze correlate --project my-app --session-id <guid>
```

APK-only endpoints (in `knowledge/endpoints.json` but not in the catalog) are flagged as "unobserved — needs runtime capture."

### Step 6: View the Dashboard

```bash
iaet explore --db catalog.db --projects .iaet-projects
```

---

## Use Case 3: Investigating a BLE Device

For Android apps that communicate with Bluetooth Low Energy devices, IAET provides a full investigation workflow: decompile the companion app, discover BLE services and characteristics, trace data flows from BLE reads/writes through to UI display, parse L2CAP channels for non-GATT devices, and correlate with HCI snoop logs from runtime.

### Step 1: Create a Project

```bash
iaet project create \
  --name my-device \
  --url ble://my-device \
  --target-type android
```

### Step 2: Decompile the Companion APK

```bash
iaet apk decompile --project my-device --apk path/to/companion-app.apk
```

### Step 3: Run APK Static Analysis

```bash
iaet apk analyze --project my-device --trace-dataflow
```

This extracts API endpoints (if any cloud backend exists) and traces network data flow through response handlers. For BLE devices, this step also identifies BLUETOOTH permissions from the manifest.

### Step 4: Discover BLE Services and Characteristics

```bash
iaet apk ble --project my-device
```

This scans decompiled Java source for:

- **Service UUIDs** — `BluetoothGattService` references, UUID constants
- **Characteristic UUIDs** — read/write/notify characteristics
- **GATT profile documentation** — maps discovered UUIDs to known Bluetooth SIG profiles
- **Command/response protocols** — byte array patterns in `writeCharacteristic` calls

**With data flow tracing:**

```bash
iaet apk ble --project my-device --trace-dataflow
```

The `--trace-dataflow` flag traces BLE characteristic values through callback chains (`onCharacteristicChanged`, `onCharacteristicRead`) to identify which characteristic drives which UI element. This produces a mapping from characteristic UUID to display label/widget.

### Step 5: L2CAP Dynamic Channel Parsing

Many BLE devices (especially audio, firmware update, and high-throughput sensor devices) use L2CAP connection-oriented channels instead of GATT for data transfer. IAET's L2CAP parser handles these non-GATT devices by extracting protocol frames from HCI snoop logs.

This happens automatically during HCI log import (Step 6) when L2CAP dynamic channels are detected.

### Step 6: Import HCI Snoop Log (Runtime Correlation)

To correlate static analysis with actual BLE traffic:

1. Enable Bluetooth HCI snoop log on the Android device (Settings > Developer Options > Enable Bluetooth HCI snoop log)
2. Use the app with the BLE device
3. Pull the log: `adb pull /data/misc/bluetooth/logs/btsnoop_hci.log`

```bash
iaet apk ble --project my-device --hci-log btsnoop_hci.log
```

This imports the HCI log and:
- Correlates runtime GATT operations with statically discovered services/characteristics
- Parses L2CAP dynamic channel frames for non-GATT data transfer
- Identifies command/response patterns from actual byte sequences
- Validates the BLE profile against real device communication

Results are written to `knowledge/ble-profile.json`.

### Step 7: Generate Client Prompt

```bash
iaet export smart-client-prompt --project my-device --language Kotlin
```

For BLE projects, the smart-client-prompt produces an adaptive prompt focused on device communication: GATT service/characteristic access, byte encoding/decoding, command/response protocols, and (if applicable) L2CAP channel management.

### Step 8: Review in Dashboard

```bash
iaet explore --db catalog.db --projects .iaet-projects
```

---

## Use Case 4: Agent-Driven Investigation

IAET includes a team of specialist Claude Code sub-agents coordinated by a **Lead Investigator** agent. The system performs multi-round investigations autonomously, asking the human only for actions that require browser interaction or credential handling.

### The Agent Team

| Agent | File | Role |
|-------|------|------|
| Lead Investigator | `agents/lead-investigator.md` | Autonomous coordinator — plans rounds, dispatches specialists, merges findings, drives to completion |
| Network Capture | `agents/network-capture.md` | HTTP and stream traffic capture via Playwright |
| Cookie & Session | `agents/cookie-session.md` | Cookie enumeration, lifecycle analysis, storage scanning |
| Crawler | `agents/crawler.md` | BFS page traversal, element discovery |
| JS Analyzer | `agents/js-analyzer.md` | Static JS bundle analysis for URL and API extraction |
| Protocol Analyzer | `agents/protocol-analyzer.md` | WebSocket, SIP, SDP, WebRTC, and HLS stream analysis |
| Schema Analyzer | `agents/schema-analyzer.md` | Dependency graphs, auth chains, rate limit detection |
| APK Analyzer | `agents/apk-analyzer.md` | Android decompilation, BLE service discovery, data flow tracing |
| API Expert | `agents/api-expert.md` | Reviews findings as an API designer; predicts missing endpoints from CRUD/pagination/batch patterns |
| Diagram Generator | `agents/diagram-generator.md` | Mermaid sequence, data flow, state machine, and dependency diagrams |
| Report Assembler | `agents/report-assembler.md` | Final export — OpenAPI, Postman, narrative, coverage |

### Autonomous Coordinator Mode

The Lead Investigator operates autonomously. When given a target, it drives the full discovery process:

**From a URL (web app):**
1. Creates the project
2. Asks for login if auth is required
3. Guides browser capture
4. Imports, analyzes, dispatches specialists (JS Analyzer, Protocol Analyzer, Schema Analyzer)
5. Runs cross-endpoint correlation
6. Auto-dispatches API Expert to predict missing endpoints
7. Opens dashboard for review at each milestone
8. Recommends completion when coverage is sufficient

**From an APK (Android/BLE):**
1. Creates the project, decompiles the APK
2. Runs static analysis with data flow tracing
3. Runs BLE analysis if Bluetooth permissions detected
4. Asks for HCI snoop log if BLE is significant
5. Imports HCI log, runs correlation
6. Generates smart client prompt
7. Dispatches API Expert for predictions

**From an app name:**
1. Asks whether to investigate web, APK, or both
2. Follows the appropriate path above

### Starting an Investigation

```bash
iaet project create --name my-target --url https://example.com --auth-required
iaet investigate --project my-target
```

Then tell Claude Code: **"Investigate the project my-target"**

### How Rounds Work

Each round follows this sequence:

1. **Lead writes a round plan** to `.iaet-projects/my-target/rounds/{NNN}-round/plan.json`
2. **Capture-stage agents run in parallel** — network capture, cookie enumeration, crawler
3. **Analysis-stage agents run in parallel** — JS analyzer, protocol analyzer, schema analyzer
4. **Lead merges findings** into `knowledge/`
5. **Lead runs correlation**: `iaet analyze correlate --project <name> --session-id <guid>`
6. **API Expert reviews** findings and predicts missing endpoints
7. **Lead opens dashboard**: `iaet explore --db catalog.db --projects .iaet-projects`
8. **Lead decides**: another round (if new endpoints found) or finalize

Check round status at any time:

```bash
iaet round status --project my-target
```

### Human-in-the-Loop Actions

The Lead Investigator will pause and ask you when:

- **Auth is required** — prompts you to log in via browser, then dispatches Cookie & Session
- **Cookies are expiring** — warns when auth cookies have less than 10 minutes remaining
- **HCI log needed** — asks you to enable Bluetooth HCI snoop log and capture BLE traffic
- **Coverage decision** — presents a summary and asks whether to continue or finalize

Store captured credentials for agent use:

```bash
iaet secrets set --project my-target --key SESSION_COOKIE --value "..."
iaet secrets set --project my-target --key CSRF_TOKEN --value "..."
```

### Finalization

When coverage is sufficient, the Lead dispatches:

1. **Diagram Generator** — Mermaid sequence, data flow, state machine, and dependency diagrams in `output/`
2. **Report Assembler** — all export formats in `output/`
3. **Smart client prompt** — `iaet export smart-client-prompt --project <name>`

Then marks the project complete:

```bash
iaet project complete --name my-target
```

To reopen for further investigation:

```bash
iaet project rerun --name my-target
```

---

## Browser Extension

### Installing

1. Navigate to `chrome://extensions` in Chrome
2. Enable **Developer mode**
3. Click **Load unpacked**
4. Select `extensions/iaet-capture/dist`

To rebuild after making changes:

```bash
cd extensions/iaet-capture
npm install
npm run build
```

### Captured Signal Types

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

### Exporting Captures

**Export as file:**
- Click **Export** in the popup — saves a `.iaet.json` file to your downloads folder

**Send directly to IAET:**
- Start the import listener: `iaet import --listen --port 7474`
- Click **Send to IAET** in the popup — the capture is imported immediately

### The .iaet.json Format

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

### Dynamic Dashboard (iaet explore)

The recommended way to view investigation results:

```bash
iaet explore --db catalog.db --projects .iaet-projects
iaet explore --db catalog.db --projects .iaet-projects --port 8080
```

Opens a local web server at `http://localhost:9200` (default) with:

- **Project selector** — choose any project from the `.iaet-projects/` directory
- **Endpoint table** — methods, paths, observation counts, schema status
- **Stream summary** — WebSocket, SSE, WebRTC connections
- **Swagger UI** — embedded OpenAPI spec browser for generated `api.yaml`
- **Next Steps** — prioritized recommendations for improving coverage
- **BLE profile** — service/characteristic inventory (for android projects)

### Static Dashboard (iaet dashboard)

Generates a self-contained HTML file:

```bash
iaet dashboard                                    # all projects → .iaet-projects/dashboard.html
iaet dashboard --project my-target                # single project
iaet dashboard --project my-target --open         # generate and open in browser
```

### Swagger UI for OpenAPI Specs

If a project has a generated `output/api.yaml`, both the dynamic and static dashboards embed a Swagger UI for it. Generate the spec first:

```bash
iaet export openapi --session-id <guid> --project my-target
```

---

## CLI Reference

Complete listing of all commands with their options.

### `iaet project`

```bash
iaet project create   --name <slug> --url <url>
                      [--target-type web|android|desktop]
                      [--auth-required]
                      [--display-name <name>]

iaet project list

iaet project status   --name <name>        # auto-detects status from captures/knowledge

iaet project archive  --name <name>

iaet project complete --name <name>        # mark investigation as done

iaet project rerun    --name <name>        # re-enable for further investigation
```

### `iaet capture`

```bash
iaet capture start    --target <name> --url <url> --session <name>
                      [--profile <browser-profile-name>]
                      [--headless]
                      [--capture-streams]          # default: enabled
                      [--capture-samples]          # capture raw frame payloads
                      [--capture-duration <sec>]
                      [--capture-frames <n>]       # max frames per stream (default: 1000)

iaet capture run      --recipe <path.ts> --session <name>
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

IAET automatically detects **protojson** responses and uses the Deep Field Resolver to infer field names from:
- Value-type heuristics (timestamps, IDs, URLs, etc.)
- Cross-endpoint correlation (shared values across responses)
- ProtoFieldMapper (`.proto` field names from decompiled APK source)

### `iaet replay`

```bash
iaet replay run    --request-id <guid>  [--dry-run]
iaet replay batch  --session-id <guid>  [--dry-run]
```

Rate limits: 10 requests/minute, 100 requests/day. Uses Polly for retry and circuit breaking.

### `iaet export`

All standard export subcommands accept `--session-id <guid>` and optionally `--output <path>` or `--project <name>`:

```bash
iaet export report              --session-id <guid>  [--output <path>]  [--project <name>]
iaet export html                --session-id <guid>  [--output <path>]  [--project <name>]
iaet export openapi             --session-id <guid>  [--output <path>]  [--project <name>]
iaet export postman             --session-id <guid>  [--output <path>]  [--project <name>]
iaet export csharp              --session-id <guid>  [--output <path>]  [--project <name>]
iaet export har                 --session-id <guid>  [--output <path>]  [--project <name>]
iaet export narrative           --session-id <guid>  [--output <path>]  [--project <name>]
iaet export client-prompt       --session-id <guid>  [--output <path>]  [--project <name>]
```

When `--project` is specified (without `--output`), the export is written to the project's `output/` directory with a canonical filename.

**Adaptive smart-client-prompt:**

```bash
iaet export smart-client-prompt --project <name>  [--language <lang>]
```

Reads the project's knowledge directory and generates an adaptive prompt for LLM-based client code generation. The prompt adapts to the project type:
- **Web projects** — HTTP client with auth, rate limiting, endpoint methods
- **BLE projects** — device communication with GATT services, byte encoding, command protocols
- **Hybrid projects** — both HTTP and BLE client generation

Default language is C#. Supported values include C#, Python, Kotlin, TypeScript, and others.

### `iaet analyze`

```bash
iaet analyze correlate  --project <name>  --session-id <guid>
```

Traces values across endpoints and streams to resolve protojson field names and discover data dependencies. Writes `knowledge/correlations.json`.

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
                   [--trace-dataflow]     # trace network responses → UI bindings (Cronet support)

iaet apk ble       --project <name>
                   [--trace-dataflow]     # trace BLE characteristic values → UI bindings
                   [--hci-log <path>]     # btsnoop_hci.log for runtime BLE correlation
```

**`apk analyze`** writes `endpoints.json`, `permissions.json`, and `network-security.json` to `.iaet-projects/<name>/knowledge/`.

**`apk ble`** writes `ble-profile.json` to `.iaet-projects/<name>/knowledge/` containing:
- Discovered service and characteristic UUIDs
- GATT profile mapping (Bluetooth SIG standard profiles)
- BLE data flow traces (characteristic → callback → UI mapping)
- L2CAP dynamic channel protocol frames (from HCI log)
- Command/response byte patterns

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
iaet dashboard                         # all projects (static HTML)
iaet dashboard --project <name>        # single project (static HTML)
iaet dashboard --project <name> --open # auto-open in browser
```

### `iaet explore`

```bash
iaet explore --db catalog.db                                    # default port: 9200
iaet explore --db catalog.db --port 8080
iaet explore --db catalog.db --projects .iaet-projects          # with project data
```

Starts the IAET Explorer web UI — a dynamic SPA for browsing sessions, endpoints, schemas, streams, project knowledge, and BLE profiles. Includes embedded Swagger UI for OpenAPI specs.

### `iaet investigate`

```bash
iaet investigate                       # guided interactive wizard (no project)
iaet investigate --project <name>      # agent-based workflow for a specific project
```

Without `--project`: interactive menu-driven wizard (capture, analyze, export). With `--project`: sets up context for the Lead Investigator agent team.

---

## Project Structure

### `.iaet-projects/` Layout

```
.iaet-projects/
  dashboard.html               <- root dashboard (all projects, static)
  my-target/
    project.json               <- project config (name, URL, target type, status)
    .env.iaet                  <- secrets (gitignored, never committed)
    captures/
      20260331-190108-capture.iaet.json.gz   <- compressed capture archives
    rounds/
      001-round/
        plan.json              <- Lead Investigator's round plan
        findings.json          <- merged specialist findings
    knowledge/
      endpoints.json           <- accumulated endpoint inventory
      cookies.json             <- cookie lifecycle summary
      protocols.json           <- stream/protocol inventory
      dependencies.json        <- auth chains and call ordering
      correlations.json        <- cross-endpoint value correlations
      permissions.json         <- APK permissions (android projects)
      network-security.json    <- cert pinning config (android projects)
      ble-profile.json         <- BLE services/characteristics (android projects)
    output/
      narrative.md             <- investigation narrative
      report.md                <- Markdown report
      api.yaml                 <- OpenAPI 3.1 spec
      collection.json          <- Postman collection
      ApiClient.cs             <- typed C# client
      diagrams/                <- Mermaid diagrams (PNG + source)
      dashboard.html           <- single-project static dashboard
    apk/                       <- android projects only
      app.apk
      mapping.txt
      decompiled/              <- jadx Java output
      resources/               <- apktool resource output
    investigation.log          <- append-only agent activity log
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

Status is auto-detected from project contents when running `iaet project status`. Possible values: `created`, `investigating`, `complete`, `archived`.

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

### Protocol Analysis

The `Iaet.ProtocolAnalysis` library provides deep analysis of captured streams:

- **SipAnalyzer** — parses SIP-over-WebSocket signaling (INVITE, ACK, BYE, PRACK), extracts call metadata, codec negotiation, and SDP session descriptions
- **WebRTC Session Reconstructor** — reconstructs full WebRTC sessions from SDP offers/answers and ICE candidates, identifies media tracks, DTLS parameters, and TURN/STUN server usage
- **WebSocket Analyzer** — frame classification, binary protocol detection, message pattern analysis
- **SDP Parser** — extracts media descriptions, codecs, ICE candidates, DTLS fingerprints
- **State Machine Builder** — infers protocol state machines from observed message sequences

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

## Legal & Ethical Guidelines

- **Rate limiting** — introduce deliberate delays between automated actions; never hammer an endpoint.
- **Credential handling** — IAET redacts `Authorization`, `Cookie`, `Set-Cookie`, and CSRF token headers before persisting. Do not disable sanitization.
- **Single-account research** — only use accounts you own or have explicit written permission to test.
- **No credential publishing** — never commit capture databases, session files, `.env.iaet`, or logs that contain authentication material.

Use IAET only on systems you own or have explicit permission to test. Unauthorized access to computer systems is illegal in most jurisdictions.
