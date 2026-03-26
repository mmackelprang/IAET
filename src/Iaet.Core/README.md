# Iaet.Core

`Iaet.Core` is the contract and domain-model layer for the IAET toolkit. It defines the interfaces and value types that all other assemblies depend on, and has **zero external NuGet dependencies** — only the .NET BCL.

## Key Interfaces

| Interface | Purpose |
|---|---|
| `ICaptureSession` | Start/stop a browser capture session and drain captured requests |
| `ICaptureSessionFactory` | Create `ICaptureSession` instances from `CaptureOptions` |
| `IEndpointCatalog` | Persist and query sessions, requests, and endpoint groups |
| `IStreamCatalog` | Persist and query `CapturedStream` records (Phase 2) |
| `IApiAdapter` | Target-specific request enrichment — implement to add domain knowledge |
| `IProtocolListener` | Attach to a CDP session to capture non-HTTP data streams |
| `IReplayEngine` | Replay stored requests (planned) |
| `ISchemaInferrer` | Infer JSON/Protobuf schemas from response bodies (planned) |
| `ICdpSession` | Thin abstraction over a Chrome DevTools Protocol session |

## Domain Models

- **`CapturedRequest`** — immutable record of a single HTTP exchange: method, URL, sanitized headers, request/response bodies, status code, duration, and session linkage.
- **`CapturedStream`** — record of a data-stream channel (WebSocket, WebRTC, HLS, etc.) with a list of `StreamFrame` payloads and a `StreamProtocol` tag.
- **`EndpointSignature`** — normalized `METHOD /path/{id}` string derived by replacing numeric IDs, GUIDs, and hex tokens in path segments with `{id}`.
- **`EndpointGroup`** — aggregation of all observations sharing the same `EndpointSignature` within a session, plus first/last-seen timestamps.
- **`CaptureSessionInfo`** — metadata for a capture session (name, target app, profile, start/stop times, request count).
- **`EndpointDescriptor`** — richer descriptor produced by an `IApiAdapter` (operation name, parameter map, auth type).
- **`Annotation`**, **`SchemaResult`**, **`ReplayResult`** — supporting models for planned features.

## Design Notes

`Iaet.Core` is intentionally dependency-free so it can be referenced by any project in the solution without dragging in Playwright, EF Core, or other heavy libraries. All concrete implementations live in sibling assemblies (`Iaet.Capture`, `Iaet.Catalog`, etc.) and are wired together by DI in `Iaet.Cli`.
