# IAET Investigation Agents

This project uses Claude Code sub-agents to investigate web applications, Android APKs, and BLE devices. The agent team follows a hybrid orchestrator + pipeline model with autonomous coordinator mode.

## Quick Start

```bash
# 1. Create a project
iaet project create --name my-target --url https://example.com --auth-required

# 2. Start an investigation (Claude Code acts as Lead Investigator)
iaet investigate --project my-target
```

Then tell Claude Code: **"Investigate the project my-target"**

The Lead Investigator runs autonomously — it plans rounds, dispatches specialists, merges findings, and only pauses when it needs human action (login, capture, device interaction).

### Key commands the agents use

```bash
# Adaptive client prompt (produces web, BLE, or hybrid prompt from project knowledge)
iaet export smart-client-prompt --project my-target --language C#

# Dynamic dashboard (live SPA for reviewing findings)
iaet explore --db catalog.db --projects .iaet-projects

# Cross-endpoint correlation (resolves protojson field names via value tracing)
iaet analyze correlate --project my-target --session-id <guid>

# Research state management
iaet project complete --name my-target
iaet project rerun    --name my-target
```

## Agent Team

| Agent | File | Role |
|-------|------|------|
| **Lead Investigator** | `agents/lead-investigator.md` | Autonomous coordinator — plans rounds, dispatches specialists, merges findings, drives discovery to completion |
| Network Capture | `agents/network-capture.md` | HTTP/stream traffic capture via Playwright |
| Cookie & Session | `agents/cookie-session.md` | Cookie enumeration, lifecycle analysis, storage scanning |
| Crawler | `agents/crawler.md` | BFS page traversal, element discovery |
| JS Analyzer | `agents/js-analyzer.md` | Static JS bundle analysis for URL/API extraction |
| Protocol Analyzer | `agents/protocol-analyzer.md` | WebSocket, SIP, SDP, WebRTC, and HLS stream analysis |
| Schema Analyzer | `agents/schema-analyzer.md` | Dependency graphs, auth chains, rate limits |
| **APK Analyzer** | `agents/apk-analyzer.md` | Android decompilation, BLE service discovery, data flow tracing |
| **API Expert** | `agents/api-expert.md` | Reviews findings as an API designer; predicts missing endpoints from patterns |
| Diagram Generator | `agents/diagram-generator.md` | Mermaid sequence, data flow, state machine, and dependency diagrams |
| Report Assembler | `agents/report-assembler.md` | Final export: OpenAPI, Postman, narrative, coverage |

## How It Works

1. The **Lead Investigator** (you talking to Claude Code) reads the project state
2. In **autonomous coordinator mode**, the Lead drives the entire discovery process:
   - For web targets: guides capture, imports, runs analysis, dispatches specialists
   - For APK targets: decompiles, runs BLE analysis, traces data flows, asks for HCI logs
   - For hybrid targets: coordinates both web and BLE investigation paths
3. For each round, the Lead dispatches specialist sub-agents via Claude Code's `Agent` tool
4. Specialists execute their tasks using IAET CLI commands and report findings
5. The Lead merges findings, updates `knowledge/`, runs cross-endpoint correlation, and opens the dashboard
6. The **API Expert** is auto-dispatched after each round to predict missing endpoints
7. On finalize, the Lead dispatches Diagram Generator and Report Assembler, then generates the smart client prompt

## Project Structure

Each investigation lives in `.iaet-projects/{name}/` with:
- `project.json` — configuration (status auto-detected from captures and knowledge)
- `.env.iaet` — secrets (gitignored, never committed)
- `rounds/` — per-round plans and findings
- `knowledge/` — accumulated structured findings (endpoints, cookies, protocols, dependencies, BLE profiles, correlations)
- `output/` — final exports and diagrams (canonical filenames when using `--project` flag)
- `apk/` — decompiled source (android projects)
- `captures/` — compressed `.iaet.json.gz` archives

## Security

- Secrets are stored in `.env.iaet` files, never in git or logs
- All exports pass through credential redaction
- Agent prompts explicitly forbid including secret values in findings
