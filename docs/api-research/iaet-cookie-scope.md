# IAET Cookie Analysis Feature — Scope

**Date:** 2026-03-29
**Context:** GV research revealed cookies are critical for headless API access but poorly understood. IAET needs first-class cookie support.

---

## Problem

Internal APIs (Google Voice, Spotify, etc.) use cookie-based auth, not OAuth2. To use these APIs headless, we need to:
1. **Capture** cookies from an authenticated browser session
2. **Analyze** which cookies are required, which rotate, and when they expire
3. **Store** them securely for headless reuse
4. **Monitor** and refresh before expiry

IAET currently captures HTTP headers (which include `Set-Cookie`) but has no dedicated cookie analysis.

---

## Proposed Features

### 1. Cookie Capture (`iaet cookies capture`)

**From Chrome debug port (CDP):**
```bash
iaet cookies capture --cdp ws://localhost:9222 --output cookies.iaet.json
```
Connects via CDP, calls `Network.getAllCookies`, saves with full metadata (domain, expiry, httpOnly, secure, sameSite, size).

**From HAR file:**
```bash
iaet cookies extract --har session.har --output cookies.iaet.json
```
Extracts cookies from HAR `request.cookies` + `response.headers` (Set-Cookie).

**From .iaet.json capture:**
Already available in request/response headers — just need a dedicated viewer.

### 2. Cookie Analysis (`iaet cookies analyze`)

```bash
iaet cookies analyze --file cookies.iaet.json
```

Output:
```
=== Cookie Analysis ===
Total: 45 cookies across 8 domains

Auth-Critical (required for API):
  SAPISID        .google.com       expires 2027-05-03 (400 days)
  SID            .google.com       expires 2027-05-03 (400 days)
  COMPASS        voice.google.com  expires 2026-04-08 (10 days) ⚠️ SHORT-LIVED

Rotation Schedule:
  PSIDRTS        daily rotation    next: tomorrow
  COMPASS        ~10 day cycle     next: 2026-04-08
  SIDCC          ~yearly           next: 2027-03-29

Not Required (tracking/analytics):
  _ga, __utm*, NID, OTZ (can be omitted for headless)

Minimum Set for API Calls:
  SID + HSID + SSID + APISID + SAPISID + COMPASS
```

### 3. Cookie Diff (`iaet cookies diff`)

```bash
iaet cookies diff --before cookies-day1.json --after cookies-day2.json
```

Shows which cookies changed, were added, or expired between captures. Critical for understanding rotation patterns.

### 4. Cookie Health Check (`iaet cookies check`)

```bash
iaet cookies check --file cookies.iaet.json --target voice.google.com
```

Makes a lightweight API call using the stored cookies to verify they're still valid. Reports which cookies are expired or will expire soon.

### 5. Cookie Export for Headless Use

```bash
iaet cookies export --file cookies.iaet.json --format curl
iaet cookies export --file cookies.iaet.json --format csharp
iaet cookies export --file cookies.iaet.json --format python
```

Generates ready-to-use code snippets with the minimum cookie set for headless API calls.

---

## Implementation Notes

### Cookie Storage Format (.iaet.json extension)

Add a `cookies` array to the existing .iaet.json format:
```json
{
  "iaetVersion": "1.0",
  "session": { ... },
  "requests": [ ... ],
  "cookies": [
    {
      "name": "SAPISID",
      "value": "ENCRYPTED_OR_REDACTED",
      "domain": ".google.com",
      "path": "/",
      "expires": "2027-05-03T00:00:00Z",
      "httpOnly": false,
      "secure": true,
      "sameSite": "None",
      "size": 34,
      "capturedAt": "2026-03-29T19:00:00Z"
    }
  ]
}
```

### CDP Integration

New `Iaet.Capture.CdpCookieExtractor` class:
```csharp
public static async Task<IReadOnlyList<CapturedCookie>> ExtractCookiesAsync(
    ICdpSession cdpSession, CancellationToken ct = default)
{
    var result = await cdpSession.SendAsync("Network.getAllCookies", ct);
    // Parse and return typed cookie objects
}
```

### CLI Commands

| Command | Phase | Priority |
|---|---|---|
| `iaet cookies capture --cdp` | Phase 1 | High |
| `iaet cookies analyze` | Phase 1 | High |
| `iaet cookies diff` | Phase 2 | Medium |
| `iaet cookies check` | Phase 2 | Medium |
| `iaet cookies export` | Phase 2 | Medium |
