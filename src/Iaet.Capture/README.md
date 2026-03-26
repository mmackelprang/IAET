# Iaet.Capture

`Iaet.Capture` is the browser-capture layer. It uses [Microsoft Playwright](https://playwright.dev/dotnet/) to launch a Chromium browser and attaches a Chrome DevTools Protocol network listener to intercept HTTP traffic produced by user interactions.

## PlaywrightCaptureSession

`PlaywrightCaptureSession` implements `ICaptureSession`. Calling `StartAsync(url)` launches Chromium (headed or headless), navigates to the starting URL, and begins recording. `GetCapturedRequestsAsync()` drains all buffered requests after you call `StopAsync()`. The session is `IAsyncDisposable`; the CLI wraps it in `await using` to guarantee the browser is closed even if the user presses Ctrl-C.

## CaptureOptions

```csharp
public sealed class CaptureOptions
{
    public required string TargetApplication { get; init; }
    public string? Profile { get; init; }   // Chromium profile directory name
    public bool Headless { get; init; }
    public StreamCaptureOptions Streams { get; init; } = new();
}
```

`Profile` is forwarded to Chromium as `--profile-directory`, which lets you reuse a logged-in browser profile so the target app does not need you to re-authenticate before capturing.

## Factory Pattern

`ICaptureSessionFactory` / `PlaywrightCaptureSessionFactory` follows the factory pattern so `Iaet.Cli` can resolve a new session per command invocation without taking a hard dependency on `PlaywrightCaptureSession`. Register via the extension method:

```csharp
services.AddIaetCapture();
```

## Stream Capture — Protocol Listeners

`PlaywrightCaptureSession` supports real-time capture of non-HTTP data streams when `CaptureOptions.Streams.Enabled` is `true`. Pass a list of `IProtocolListener` implementations to the 3-argument factory overload:

```csharp
var session = factory.Create(options, streamCatalog, listeners);
```

During `StartAsync`, a `PlaywrightCdpSession` is created and each listener's `AttachAsync` is called. During `StopAsync`, `DetachAsync` is called and all pending streams are persisted to `IStreamCatalog`.

Five built-in listeners are provided:

| Listener | Detected by |
|---|---|
| `WebSocketListener` | `Network.webSocketCreated` CDP events |
| `SseListener` | `text/event-stream` content-type |
| `MediaStreamListener` | `.m3u8` / `.mpd` URLs or `mpegurl` / `dash+xml` content-types |
| `GrpcWebListener` | `grpc-web` or `x-protobuf` content-types |
| `WebRtcListener` | `WebRTC.peerConnectionCreated` CDP events |

## StreamCaptureOptions

```csharp
public sealed class StreamCaptureOptions
{
    public bool Enabled { get; init; }                    // master switch
    public bool CaptureSamples { get; init; }             // record frame payloads
    public int MaxFramesPerConnection { get; init; } = 1000;
    public int SampleDurationSeconds { get; init; } = 10;
    public int MaxMediaSegments { get; init; } = 3;
    public string? SampleOutputDirectory { get; init; }
}
```

When `CaptureSamples` is `false` (the default), listeners track metadata only (counts, URLs, protocol) without buffering payload bytes.

## Implementing a Custom IProtocolListener

```csharp
public sealed class MyCustomListener : IProtocolListener
{
    public string ProtocolName => "MyProtocol";
    public StreamProtocol Protocol => StreamProtocol.Unknown;
    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog,
        CancellationToken ct = default)
    {
        await cdpSession.SubscribeToDomainAsync("Network", ct);
        // subscribe to relevant CDP events via cdpSession.OnEvent(...)
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        // return all CapturedStream records accumulated so far
        return [];
    }
}
```

Register your custom listener in DI and resolve it alongside the built-in ones when creating the session.

## RequestSanitizer

`RequestSanitizer.SanitizeHeaders` strips credential-bearing headers before any request is handed to the catalog. The redacted set is: `Authorization`, `Cookie`, `Set-Cookie`, `X-CSRF-Token`, `X-XSRF-Token`, `X-Goog-AuthUser`. The value is replaced with the literal string `<REDACTED>`. The set is internal and cannot be shrunk at runtime — this is intentional to prevent accidental credential leakage.
