# IAET UAT Progress Log

## Session: 2026-03-27

### Phase A: Capture — COMPLETE
- Headless captures for all 4 targets: GV (6), Spotify (33), Maps (75), Facebook (5)
- Found Bug #1: WebRTC.enable CDP crash — fixed with try-catch
- All captures successful after fix

### Phase B: Catalog/Schema/Streams — COMPLETE
- All session catalogs verified
- Endpoint normalization working ({id} placeholders)
- Schema inference successful: JSON Schema, C# Record, OpenAPI Fragment
- Spotify masthead schema: rich nested types
- GV CMS query schema: paginated items with metadata

### Phase C: Export Suite — COMPLETE (partial)
- GV: All 6 formats PASS (report, html, openapi, postman, csharp, har)
- Spotify/Maps/Facebook: ALL FAIL — Bug #2 (JsonReaderException on HTML bodies)
- Credential redaction audit: PASS for GV (all `cookie: <REDACTED>`)
- Stdout export: PASS

### Phase D: Replay — COMPLETE
- Dry-run single/batch: PASS
- Live replay against GV: PASS
- Replay via Explorer API: FAIL (Bug #3 — HTTP 500)

### Phase E: Explorer Web UI — COMPLETE (API-only)
- All REST API endpoints tested via curl
- Sessions, endpoints, schema, export download, streams: all PASS
- Replay API: FAIL (Bug #3)
- Playwright MCP browser testing blocked by permissions
- Error handling: 404 for bad session, 400 for bad format — PASS

### Phase F: Crawler — COMPLETE
- Basic crawl: PASS
- Blacklist: PASS
- Duration limit: PASS
- JSON output: PASS

### Phase G: Import — COMPLETE
- File import: PASS
- Listener mode + curl POST: PASS
- Error on nonexistent file: PASS

### Phase H: Error Handling — COMPLETE
- All 6 error scenarios handled gracefully, no stack traces

### Phase I: Wizard — COMPLETE
- Piped stdin test: PASS (banner, prompts, import method, clean exit)

### Phase J: UAT Plan Update — COMPLETE
- docs/uat-plan.md rewritten as AI-first v2.0
- 74/79 AI-automatable steps passed
- 28 human-only steps identified and documented
- 2 open bugs, 2 fixed bugs documented

### Summary
- **Total steps tested by AI: 79**
- **Passed: 74 (94%)**
- **Failed: 5 (6%) — all due to 2 open bugs**
- **Human-only: 28 steps remaining**
