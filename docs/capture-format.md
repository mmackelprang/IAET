# IAET Capture Format (.iaet.json)

Version: **1.0**

`.iaet.json` is the portable interchange format for IAET capture data. It allows sessions to be exported from one machine and imported, replayed, or analyzed on another without access to the originating SQLite catalog.

---

## Encoding Rules

- **Encoding**: UTF-8, no BOM.
- **Credential redaction**: All header values that were redacted during capture appear as the literal string `"<REDACTED>"`. Consumers must treat any `<REDACTED>` value as absent data, not as an actual header value.
- **Source identification**: The `session.capturedBy` field SHOULD contain the IAET version string that produced the file. Consumers use this to detect forward-compatibility issues.
- **Timestamps**: All timestamps are ISO 8601 strings with UTC offset (`2025-03-26T14:00:00.000Z`).
- **Optional fields**: Fields marked optional may be `null` or omitted entirely. Consumers must treat absent and `null` identically.

---

## Top-Level Object

```jsonc
{
  "iaetVersion": "1.0",          // Format version — always "1.0" for this spec
  "exportedAt": "<timestamp>",   // When this file was produced
  "session": { ... },            // Single session object
  "requests": [ ... ],           // Array of captured HTTP requests
  "streams": [ ... ]             // Array of captured data streams (Phase 2; may be empty)
}
```

---

## Session Object

```jsonc
{
  "id": "<uuid>",                      // Session GUID
  "name": "my-session",               // Human-readable session label
  "targetApplication": "App Name",    // --target value
  "profile": "default",               // Browser profile name (optional)
  "startedAt": "<timestamp>",         // When capture started
  "stoppedAt": "<timestamp>",         // When capture stopped (optional; null if interrupted)
  "capturedBy": "iaet/0.1.0"          // Producer identification string
}
```

---

## Requests Array

Each element represents one completed HTTP exchange:

```jsonc
{
  "id": "<uuid>",                       // Request GUID
  "sessionId": "<uuid>",               // Parent session GUID
  "timestamp": "<timestamp>",          // When the request was initiated
  "httpMethod": "GET",                 // HTTP verb, uppercase
  "url": "https://example.com/api/v1/users/42",  // Full URL including query string
  "requestHeaders": {                  // Sanitized request headers (key: value)
    "Content-Type": "application/json",
    "Authorization": "<REDACTED>"
  },
  "requestBody": null,                 // Request body as UTF-8 string (optional)
  "responseStatus": 200,              // HTTP response status code
  "responseHeaders": {                 // Sanitized response headers
    "Content-Type": "application/json"
  },
  "responseBody": "{ ... }",          // Response body as UTF-8 string (optional)
  "durationMs": 123,                  // Round-trip duration in milliseconds
  "tag": null                          // Optional user-assigned tag string
}
```

---

## Streams Array (Phase 2)

The `streams` array is reserved for Phase 2 data-stream capture. Consumers reading a v1.0 file SHOULD ignore unknown fields and MAY treat an absent or empty `streams` array as equivalent.

Each element represents one data-stream channel:

```jsonc
{
  "id": "<uuid>",
  "sessionId": "<uuid>",
  "protocol": "WebSocket",           // One of: WebSocket, ServerSentEvents, WebRtc,
                                     //   HlsStream, DashStream, GrpcWeb, WebAudio, Unknown
  "url": "wss://example.com/ws",
  "startedAt": "<timestamp>",
  "endedAt": "<timestamp>",          // Optional; null if stream was open at session end
  "metadata": {                      // Protocol-specific key/value properties
    "subprotocol": "graphql-ws"
  },
  "frames": [                        // Optional; may be absent for large streams
    {
      "timestamp": "<timestamp>",
      "direction": "Received",       // "Sent" or "Received"
      "textPayload": "{ ... }",      // Optional; present for text frames
      "binaryPayload": null,         // Optional; base64-encoded bytes for binary frames
      "sizeBytes": 512
    }
  ],
  "samplePayloadPath": null,         // Optional relative path to a sample payload file
  "tag": null
}
```

---

## Versioning

If the format evolves, `iaetVersion` will be incremented. IAET importers SHOULD warn (but not fail) when reading a file whose `iaetVersion` is newer than the importer supports. Future versions will be additive where possible.
