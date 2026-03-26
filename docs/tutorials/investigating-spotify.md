# Tutorial: Investigating the Spotify Web Player API

This tutorial walks through a complete IAET investigation of the Spotify Web Player.
By the end you will have captured all HTTP traffic and WebSocket streams, viewed the
endpoint catalog, inferred JSON schemas, and exported the results as both an OpenAPI
specification and a Markdown report.

---

## Prerequisites

- .NET 10 SDK
- A Spotify account (free or premium)
- Chrome or Chromium installed

---

## Step 1 — Install IAET

```bash
dotnet tool install -g iaet
iaet --version
# 0.1.0
```

---

## Step 2 — Start a Capture Session

Open a capture session targeting the Spotify Web Player. IAET will launch a
Chromium instance with the Chrome DevTools Protocol (CDP) attached so that every
HTTP request and WebSocket connection is intercepted and stored.

```bash
iaet capture start \
  --target "Spotify" \
  --url https://open.spotify.com \
  --session spotify-2026-03-26 \
  --capture-streams \
  --capture-samples \
  --capture-frames 500
```

Expected output:

```
Starting capture session 'spotify-2026-03-26' for Spotify...
Browser will open. Perform actions, then press Enter to stop.
Stream capture enabled (WebSocket, SSE, WebRTC, HLS/DASH, gRPC-Web).
Recording... Session ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

In the browser, log in, play a song, open the search page, and navigate to your
library. Each UI action triggers additional API calls. When you are satisfied, press
Enter in the terminal.

```
Captured 312 requests.
```

---

## Step 3 — List Capture Sessions

```bash
iaet catalog sessions
```

```
ID                                     Name                  Target               Requests   Started
--------------------------------------------------------------------------------------------------------------
a1b2c3d4-e5f6-7890-abcd-ef1234567890   spotify-2026-03-26    Spotify              312        3/26/2026 14:05
```

Copy the session ID for use in subsequent commands.

---

## Step 4 — View Discovered Endpoints

```bash
iaet catalog endpoints --session-id a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

```
Endpoint                                           Count    First Seen             Last Seen
---------------------------------------------------------------------------------------------------------
GET /v1/me                                         4        3/26/2026 14:05:12     3/26/2026 14:08:01
GET /v1/me/player                                  18       3/26/2026 14:05:14     3/26/2026 14:09:45
GET /v1/me/playlists                               2        3/26/2026 14:05:16     3/26/2026 14:06:02
GET /v1/search                                     5        3/26/2026 14:06:30     3/26/2026 14:07:50
GET /v1/tracks/{id}                                11       3/26/2026 14:05:20     3/26/2026 14:09:30
PUT /v1/me/player/play                             3        3/26/2026 14:06:01     3/26/2026 14:08:55
PUT /v1/me/player/pause                            2        3/26/2026 14:07:10     3/26/2026 14:09:00
```

IAET automatically normalizes path parameters — concrete track IDs like
`/v1/tracks/4iV5W9uYEdYUVa79Axb7Rh` are collapsed to `/v1/tracks/{id}`.

---

## Step 5 — View WebSocket Streams

Spotify uses a persistent WebSocket connection to push real-time state changes —
track progress, playback events, and remote device updates — back to the browser.

```bash
iaet streams list --session-id a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

```
ID                                     Protocol    URL
------------------------------------------------------------------------------------------------------------
f7a3b291-cc12-4e56-9012-3456789abcde   WebSocket   wss://dealer.spotify.com/?access_token=<REDACTED>
```

```bash
iaet streams show --stream-id f7a3b291-cc12-4e56-9012-3456789abcde
```

```
Protocol : WebSocket
URL      : wss://dealer.spotify.com/?access_token=<REDACTED>
Frames   : 147
Metadata : compression=permessage-deflate, subprotocol=
```

The `access_token` query parameter is automatically redacted by IAET's header
sanitizer so captured data is safe to share.

---

## Step 6 — Infer Schemas from Response Bodies

```bash
iaet schema infer \
  --session-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --endpoint "GET /v1/me"
```

```
=== JSON Schema ===
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "id": { "type": "string" },
    "display_name": { "type": "string" },
    "email": { "type": "string" },
    "country": { "type": "string" },
    "product": { "type": "string" }
  }
}

=== C# Record ===
public sealed record GetV1MeResponse(
    string? Id,
    string? DisplayName,
    string? Email,
    string? Country,
    string? Product
);

=== OpenAPI Fragment ===
type: object
properties:
  id:
    type: string
  display_name:
    type: string
  ...
```

---

## Step 7 — Export as OpenAPI

Generate a full OpenAPI 3.1 YAML specification from the entire session:

```bash
iaet export openapi \
  --session-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --output spotify-api.yaml
```

```
OpenAPI spec written to spotify-api.yaml
```

Open `spotify-api.yaml` in [Swagger Editor](https://editor.swagger.io/) or import
it into Postman to explore the discovered API interactively.

---

## Step 8 — Export as Investigation Report

Generate a human-readable Markdown report that summarizes all findings:

```bash
iaet export report \
  --session-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --output spotify-report.md
```

```
Markdown report written to spotify-report.md
```

The report contains:

- Session metadata (date, target, request count)
- Full endpoint catalog table
- Per-endpoint detail sections with example request/response and inferred C# record
- Data stream summary (WebSocket connections captured)
- Generation timestamp

For a self-contained HTML version suitable for sharing with a team:

```bash
iaet export html \
  --session-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --output spotify-report.html
```

---

## What to Try Next

- Export a Postman collection and run the requests in Postman:
  `iaet export postman --session-id <id> --output spotify.postman_collection.json`
- Export a typed C# client to use the API in a .NET project:
  `iaet export csharp --session-id <id> --output SpotifyClient.cs`
- Export a HAR file for use in browser DevTools or external analysis tools:
  `iaet export har --session-id <id> --output spotify.har`
- Write a `SpotifyAdapter` to enrich endpoint names with Spotify-domain knowledge
  (see [adapter-guide.md](../adapter-guide.md))
