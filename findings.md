# IAET UAT Findings

## Session Registry

| Session Name | Target | Session ID | Request Count | Notes |
|---|---|---|---|---|
| gv-uat-02 | GV | 6011c6e3-0aa5-4db5-b589-a9674f51ee87 | 6 | Headless, 15s capture |
| spotify-uat-01 | Spotify | ea68134a-8ebe-4f06-a58b-0a8fefe5e9b3 | 33 | Headless, public traffic only |
| gmaps-uat-01 | Google Maps | d2c74637-3a3a-4d0c-9544-e881526fb211 | 75 | Headless, chatty tile traffic |
| fb-uat-01 | Facebook | 2feea0d2-f0b3-4a4d-9891-1931bbda9af6 | 5 | Headless, login-wall limited |
| import-test-01 | Test Import | aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee | 1 | Manual .iaet.json import |
| import-test-02-listener | Test Listener | aaaaaaaa-bbbb-cccc-dddd-ffffffffffff | 1 | HTTP listener import |

## Bugs Found

| # | Severity | Component | Location | Description | Status |
|---|---|---|---|---|---|
| 1 | Critical | WebRtcListener | `Listeners/WebRtcListener.cs:38` | `WebRTC.enable` CDP domain not available — PlaywrightException crashes capture | **Fixed** |
| 2 | High | JsonTypeMap | `Schema/JsonTypeMap.cs:53` | `JsonDocument.Parse` on non-JSON bodies (HTML, JSONP, FB `for(;;);` prefix) | Open |
| 3 | Medium | ReplayApi | `Explorer/Api/ReplayApi.cs:22` | Unhandled exception → HTTP 500 with empty body | Open |
| 4 | Low | PageInteractor | `Crawler/PageInteractor.cs:42` | `/path` → `file:///path` on Linux | **Fixed** |
| 5 | High | JsonDiffer | `Replay/JsonDiffer.cs:44` | Same as #2 — `JsonDocument.Parse` on non-JSON during diff. Also crashes batch, aborting remaining requests | Open |
| 6 | Medium | CrawlCommand | CLI crawl handler | `PlaywrightPageNavigator` not wired into `CrawlEngine`. All crawls are dry-run placeholder only | Open |
| 7 | Low | CLI logging | Program.cs / Serilog config | INF logs go to stdout, mixing with export output in stdout mode | Open |

## Non-JSON Response Body Variants Observed

| Target | Body Prefix | Content-Type | Notes |
|---|---|---|---|
| Spotify | `<` (HTML) | text/html | Login/redirect pages |
| Google Maps | `)]}'\n` | application/json (JSONP) | XSS protection prefix on JSON |
| Facebook | `for (;;);{` | application/json | XSS protection prefix on JSON |
| GV (some) | UTF-8 BOM `0xEF BB BF` | application/json | BOM before valid JSON |

## Credential Redaction Audit

| Export File | Sensitive Patterns Found | Status |
|---|---|---|
| gv-report.md | 4x `cookie: <REDACTED>` | CLEAN |
| gv-report.html | 4x `cookie: <REDACTED>` | CLEAN |
| gv-api.yaml | None | CLEAN |
| gv.postman.json | None | CLEAN |
| GvClient.cs | None | CLEAN |
| gv.har | None (cookie values JSON-escaped as `\u003CREDACTED\u003E`) | CLEAN |
| Spotify exports | N/A — export crashed (Bug #2) | BLOCKED |
| Google Maps exports | N/A — export crashed (Bug #2) | BLOCKED |
| Facebook exports | N/A — export crashed (Bug #2) | BLOCKED |

## Export Format Validation

| File | Format | Valid | Size | Details |
|---|---|---|---|---|
| gv-api.yaml | OpenAPI 3.1 | Yes | 42KB | Starts with `openapi: '3.1.0'` |
| gv.postman.json | Postman v2.1 | Yes | 11KB | Valid JSON, importable |
| gv.har | HAR 1.2 | Yes | 754KB | 6 entries in `log.entries` |
| gv-report.html | HTML | Yes | 197KB | `<!DOCTYPE html>` |
| GvClient.cs | C# | Yes | 13KB | 47 class/record definitions, `GeneratedClientApiClient` |
| gv-report.md | Markdown | Yes | 191KB | `# API Investigation Report` with full structure |

## Error Handling Audit

| Scenario | Exit Code | Output | Stack Trace? | Result |
|---|---|---|---|---|
| Nonexistent session ID | 0 | `No endpoints found.` | No | PASS |
| Nonexistent endpoint | 0 | `No response bodies found for the specified session/endpoint.` | No | PASS |
| Nonexistent request ID | 0 | `Request 00000000-... not found.` | No | PASS |
| Nonexistent database | 0 | `Error: database file not found: nonexistent.db` | No | PASS |
| Nonexistent import file | 0 | `File not found: D:\prj\IAET\nonexistent.json` | No | PASS |

**Note:** All error scenarios return exit code 0. Returning non-zero for errors would be more conventional but was not required.

## Import Pipeline Audit

| Test | Result | Notes |
|---|---|---|
| File import (.iaet.json) | PASS | `Imported session 'import-test-01' (1 requests, 1 session record).` |
| HTTP listener import | PASS | `{"ok":true,"requestCount":1,"sessionName":"import-test-02-listener"}` |
| Duplicate ID import | PASS | Correctly rejected with descriptive error |
| Catalog verification | PASS | Both sessions visible in `catalog sessions` |

## Crawler Audit

| Test | Options Parsed | Live Crawl | Output File | Result |
|---|---|---|---|---|
| max-depth 2, max-pages 5 | Yes (echoed in banner) | No (dry-run) | N/A | PARTIAL |
| --blacklist "/portfolio/*" | Yes (echoed in banner) | No (dry-run) | N/A | PARTIAL |
| --max-duration 15 | Yes (shown as "15s") | No (dry-run) | N/A | PARTIAL |
| --output crawl-report.json | Yes | No (dry-run) | Valid JSON placeholder (408B) | PARTIAL |
