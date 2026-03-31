# IAET Investigation Agents

This project uses Claude Code sub-agents to investigate web applications and document their back-end services. The agent team follows a hybrid orchestrator + pipeline model.

## Quick Start

```bash
# 1. Create a project
iaet project create --name my-target --url https://example.com --auth-required

# 2. Start an investigation (Claude Code acts as Lead Investigator)
iaet investigate --project my-target
```

Then tell Claude Code: **"Investigate the project my-target following the Lead Investigator protocol in agents/lead-investigator.md"**

## Agent Team

| Agent | File | Role |
|-------|------|------|
| **Lead Investigator** | `agents/lead-investigator.md` | Orchestrator — plans rounds, dispatches specialists, merges findings |
| Network Capture | `agents/network-capture.md` | HTTP/stream traffic capture via Playwright |
| Cookie & Session | `agents/cookie-session.md` | Cookie enumeration, lifecycle analysis, storage scanning |
| Crawler | `agents/crawler.md` | BFS page traversal, element discovery |
| JS Analyzer | `agents/js-analyzer.md` | Static JS bundle analysis for URL/API extraction |
| Protocol Analyzer | `agents/protocol-analyzer.md` | WebSocket/SDP/HLS stream analysis |
| Schema Analyzer | `agents/schema-analyzer.md` | Dependency graphs, auth chains, rate limits |
| Diagram Generator | `agents/diagram-generator.md` | Mermaid sequence/flow/state diagrams |
| Report Assembler | `agents/report-assembler.md` | Final export: OpenAPI, Postman, narrative, coverage |

## How It Works

1. The **Lead Investigator** (you talking to Claude Code) reads the project state
2. For each round, the Lead dispatches specialist sub-agents via Claude Code's `Agent` tool
3. Specialists execute their tasks using IAET CLI commands and report findings
4. The Lead merges findings, updates `knowledge/`, and decides: another round or finalize?
5. On finalize, the Lead dispatches Diagram Generator and Report Assembler

## Project Structure

Each investigation lives in `.iaet-projects/{name}/` with:
- `project.json` — configuration
- `.env.iaet` — secrets (gitignored, never committed)
- `rounds/` — per-round plans and findings
- `knowledge/` — accumulated structured findings
- `output/` — final exports and diagrams

## Security

- Secrets are stored in `.env.iaet` files, never in git or logs
- All exports pass through credential redaction
- Agent prompts explicitly forbid including secret values in findings
