# IAET — Internal API Extraction Toolkit

IAET is a toolkit for discovering, capturing, analyzing, and documenting undocumented APIs from web applications, Android APKs, and BLE devices. It intercepts HTTP traffic via the Chrome DevTools Protocol or a browser extension, decompiles APKs to extract endpoints and BLE services, traces data flows through source code, and persists everything to a local SQLite catalog for analysis and export.

Intended for educational and security research purposes only.

---

## Quick Start

### Install from source

```bash
# Build and install the iaet global tool
bash scripts/install.sh      # Linux / macOS / Git Bash
pwsh scripts/install.ps1     # Windows PowerShell
```

Or install manually:

```bash
dotnet pack src/Iaet.Cli/Iaet.Cli.csproj -c Release -o artifacts/
dotnet tool install -g Iaet.Cli --add-source artifacts/ --version 0.1.0
```

Verify:

```bash
iaet --version
```

### Web application investigation

```bash
# 1. Create a project
iaet project create --name my-target --url https://example.com --auth-required

# 2. Load the browser extension (extensions/iaet-capture/dist) in Chrome
# 3. Browse the target, click Stop, Export → saves capture.iaet.json

# 4. Import the capture
iaet import --file capture.iaet.json --project my-target

# 5. Generate outputs
iaet export openapi  --session-id <guid> --project my-target
iaet export narrative --session-id <guid> --project my-target
iaet export smart-client-prompt --project my-target
iaet analyze correlate --project my-target --session-id <guid>

# 6. Open the dynamic dashboard
iaet explore --db catalog.db --projects .iaet-projects
```

### Android APK + BLE device investigation

```bash
iaet project create --name my-device --url ble://device --target-type android
iaet apk decompile --project my-device --apk path/to/app.apk
iaet apk analyze   --project my-device --trace-dataflow
iaet apk ble       --project my-device --trace-dataflow
iaet apk ble       --project my-device --hci-log btsnoop_hci.log
iaet export smart-client-prompt --project my-device
iaet explore --db catalog.db --projects .iaet-projects
```

### Agent-driven investigation

```bash
iaet project create --name my-target --url https://example.com --auth-required
iaet investigate --project my-target
# Then tell Claude Code: "Investigate the project my-target"
# The Lead Investigator drives the process autonomously
```

See **[docs/user-guide.md](docs/user-guide.md)** for comprehensive step-by-step instructions covering all workflows.

---

## Features

- **Browser extension** — captures HTTP (fetch/XHR), WebSocket (with binary frame decoding), WebRTC (SDP/ICE), and SSE from any Chrome tab without a proxy
- **Playwright-based capture** — CDP session recording with stream monitoring (WebSocket, SSE, WebRTC, HLS, DASH, gRPC-Web)
- **Android APK analysis** — jadx decompilation, endpoint extraction, auth pattern detection, manifest permissions, network security config (cert pinning), network data flow tracing (Cronet support)
- **BLE device analysis** — service/characteristic UUID discovery from decompiled source, L2CAP dynamic channel parsing for non-GATT devices, BLE data flow tracing (characteristic to UI mapping), HCI snoop log import for runtime correlation
- **Protocol analysis** — SipAnalyzer for SIP-over-WebSocket signaling, WebRTC session reconstruction from SDP/ICE, protojson field name inference (Deep Field Resolver with endpoint context and APK source)
- **Cross-endpoint correlation** — value tracing across endpoints and streams to resolve protojson field names and discover data dependencies
- **Agent investigation system** — autonomous multi-round investigation with 11 specialist Claude Code agents including APK Analyzer, API Expert, and Lead Investigator with autonomous coordinator mode
- **SQLite endpoint catalog** — persistent storage with automatic deduplication and observation counting
- **Schema inference** — JSON Schema (draft-07), C# records, and OpenAPI 3.1 fragments; detects protojson with ProtoFieldMapper for name recovery from decompiled APK source
- **HTTP replay** — field-level JSON diff, pluggable auth provider, rate limiting, Polly retry + circuit breaker, dry-run mode
- **Export** — Markdown report, HTML, OpenAPI 3.1 YAML, Postman v2.1.0, typed C# client, HAR 1.2, narrative, AI client-generation prompt, adaptive smart-client-prompt (web, BLE, or hybrid)
- **Dynamic dashboard** — `iaet explore --db catalog.db --projects .iaet-projects` serves a live SPA with project selection, Swagger UI, and next-steps recommendations
- **Static dashboard** — `iaet dashboard` generates a self-contained HTML overview
- **Research state management** — `iaet project complete` and `iaet project rerun` for marking investigation lifecycle; auto-detect project status from captures and knowledge
- **Canonical export filenames** — `--project` flag on export commands writes outputs to the project's `output/` directory with consistent names
- **Compressed capture archival** — `iaet import --project` stores captures as `.iaet.json.gz`
- **Secrets management** — per-project `.env.iaet` file; never committed to git
- **Semi-autonomous crawler** — BFS page traversal with configurable depth, blacklists, and TypeScript recipe execution
- **Cookie analysis** — lifecycle tracking, rotation detection, expiry warnings across snapshots
- **Diagrams** — Mermaid sequence, data flow, state machine, dependency graph, and confidence-annotated diagrams

---

## CLI Reference

```
iaet
├── project
│   ├── create   --name <slug>  --url <url>
│   │            [--target-type web|android|desktop]  [--auth-required]
│   │            [--display-name <name>]
│   ├── list
│   ├── status   --name <name>
│   ├── archive  --name <name>
│   ├── complete --name <name>
│   └── rerun    --name <name>
│
├── capture
│   ├── start  --target <name>  --url <url>  --session <name>
│   │          [--profile <name>]  [--headless]
│   │          [--capture-streams]  [--capture-samples]
│   │          [--capture-duration <seconds>]  [--capture-frames <n>]
│   └── run    --recipe <path>  --session <name>
│
├── import     --file <path.iaet.json>  [--project <name>]
│             |--listen  [--port <n>]   (default port: 7474)
│
├── catalog
│   ├── sessions
│   └── endpoints  --session-id <guid>
│
├── streams
│   ├── list    --session-id <guid>
│   ├── show    --stream-id <guid>
│   └── frames  --stream-id <guid>
│
├── schema
│   ├── infer  --session-id <guid>  --endpoint <signature>
│   └── show   --session-id <guid>  --endpoint <signature>
│              --format <json|csharp|openapi>
│
├── replay
│   ├── run    --request-id <guid>  [--dry-run]
│   └── batch  --session-id <guid>  [--dry-run]
│
├── export
│   ├── report              --session-id <guid>  [--output <path>]  [--project <name>]
│   ├── html                --session-id <guid>  [--output <path>]  [--project <name>]
│   ├── openapi             --session-id <guid>  [--output <path>]  [--project <name>]
│   ├── postman             --session-id <guid>  [--output <path>]  [--project <name>]
│   ├── csharp              --session-id <guid>  [--output <path>]  [--project <name>]
│   ├── har                 --session-id <guid>  [--output <path>]  [--project <name>]
│   ├── narrative           --session-id <guid>  [--output <path>]  [--project <name>]
│   ├── client-prompt       --session-id <guid>  [--output <path>]  [--project <name>]
│   └── smart-client-prompt --project <name>  [--language <lang>]
│
├── analyze
│   └── correlate  --project <name>  --session-id <guid>
│
├── crawl      --url <url>  [--target <name>]  [--session <name>]
│              [--max-depth <n>]  [--max-pages <n>]  [--max-duration <seconds>]
│              [--headless]  [--blacklist <pattern>]...
│              [--exclude-selector <css>]...  [--output <path>]
│
├── apk
│   ├── decompile  --project <name>  --apk <path>
│   │              [--jadx-path <path>]  [--mapping <mapping.txt>]
│   ├── analyze    --project <name>  [--trace-dataflow]
│   └── ble        --project <name>  [--trace-dataflow]  [--hci-log <path>]
│
├── cookies
│   ├── snapshot  --project <name>
│   ├── diff      --project <name>  --before <guid>  --after <guid>
│   └── analyze   --project <name>
│
├── secrets
│   ├── set    --project <name>  --key <key>  --value <value>
│   ├── get    --project <name>  --key <key>
│   ├── list   --project <name>
│   └── audit  --project <name>
│
├── round
│   └── status  --project <name>
│
├── dashboard  [--project <name>]  [--open]
├── explore    --db <path>  [--port <n>]  [--projects <path>]
└── investigate [--project <name>]
```

---

## Architecture

See **[docs/architecture.md](docs/architecture.md)** for the full dependency diagram and data flow.

---

## Legal & Ethical Guidelines

- **Rate limiting** — introduce deliberate delays between automated actions.
- **Credential handling** — IAET redacts `Authorization`, `Cookie`, `Set-Cookie`, and CSRF token headers before persisting. Do not disable sanitization.
- **Single-account research** — only use accounts you own or have explicit written permission to test.
- **No credential publishing** — never commit capture databases, session files, `.env.iaet`, or logs that contain authentication material.

Use IAET only on systems you own or have explicit permission to test. Unauthorized access to computer systems is illegal in most jurisdictions.

---

## Development

```bash
git clone https://github.com/mmackelprang/IAET.git
cd IAET

dotnet build Iaet.slnx
dotnet test  Iaet.slnx

# Pack NuGet packages
pwsh scripts/build.ps1 -Target pack
```

Artifacts are written to `artifacts/`.

---

## License

MIT — see [LICENSE](LICENSE).
