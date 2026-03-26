# Iaet.Replay

`Iaet.Replay` implements `IReplayEngine` to re-issue stored HTTP requests against their original URLs, compare live responses to captured ones, and surface field-level diffs.

---

## Purpose

Given a `CapturedRequest` from the IAET catalog, `Iaet.Replay` will:

1. Rebuild the original HTTP request (method, URL, headers, body) — skipping redacted credential headers.
2. Optionally apply fresh authentication via a pluggable `IReplayAuthProvider`.
3. Send the request through an `HttpClient` backed by Polly retry and circuit-breaker policies.
4. Compare the live response body against the captured one using JSONPath-based field-level diffing.
5. Return a `ReplayResult` containing the status code, diffs, and wall-clock duration.

---

## Key Types

### `IReplayEngine` (from `Iaet.Core.Abstractions`)

```csharp
Task<ReplayResult> ReplayAsync(CapturedRequest original, CancellationToken ct = default);
```

Returns `ReplayResult(ResponseStatus, ResponseBody, Diffs, DurationMs)`. Each `FieldDiff` carries the JSONPath `Path`, the `Expected` value (from the captured response), and the `Actual` value (from the live response).

### `JsonDiffer`

Static helper that produces a flat list of `FieldDiff` records by recursively comparing two JSON strings using JSONPath dot-notation paths.

```csharp
var diffs = JsonDiffer.Diff(capturedBody, liveBody);
```

Added, removed, and changed fields are all reported. Fields are compared by value; structural changes produce multiple diffs.

### `IReplayAuthProvider` (from `Iaet.Core.Abstractions`)

```csharp
Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default);
```

Implement this interface and register it in DI to inject fresh credentials (e.g., Bearer tokens) before each replayed request. If no provider is registered, requests are sent as captured (with redacted headers omitted).

### `ReplayOptions`

| Property | Default | Description |
|---|---|---|
| `RequestsPerMinute` | 10 | Per-minute rate limit |
| `RequestsPerDay` | 100 | Per-day rate limit |
| `TimeoutSeconds` | 30 | Per-request HTTP timeout |
| `DryRun` | false | Skip HTTP calls; return empty result |

---

## Rate Limiting

Two independent `FixedWindowRateLimiter` windows (per-minute and per-day) guard all outbound requests. When either limit is exceeded an `InvalidOperationException` is thrown and the request is not sent. Configure the limits via `ReplayOptions` when calling `AddIaetReplay`.

---

## Dependency Injection

```csharp
// Default options (10 req/min, 100 req/day)
services.AddIaetReplay();

// Custom options
services.AddIaetReplay(opts =>
{
    opts = opts with { RequestsPerMinute = 5, RequestsPerDay = 50 };
});
```

`AddIaetReplay` wires up `HttpReplayEngine` with `IHttpClientFactory` and attaches the standard Microsoft resilience handler (Polly retry + circuit breaker).

Register an optional auth provider:

```csharp
services.AddSingleton<IReplayAuthProvider, MyBearerTokenProvider>();
```

---

## CLI Usage

```bash
# Replay a single request and show diffs
iaet replay run --request-id <guid>

# Dry-run: print what would be sent without making HTTP calls
iaet replay run --request-id <guid> --dry-run

# Replay one representative request per unique endpoint in a session
iaet replay batch --session-id <guid>
iaet replay batch --session-id <guid> --dry-run
```
