# IAET Phase 2: Stream Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend IAET's capture engine to detect and record WebSocket, SSE, WebRTC signaling, HLS/DASH manifests, and gRPC-Web/Protobuf traffic alongside existing HTTP capture — with selective payload sampling.

**Architecture:** Implement `ICdpSession` as a real CDP wrapper in Iaet.Capture. Build protocol-specific `IProtocolListener` implementations that attach to CDP events. Extend `SqliteCatalog` to implement `IStreamCatalog`. Add `iaet streams` CLI commands. All work on a feature branch with PR.

**Tech Stack:** .NET 10, Playwright .NET (CDP access), EF Core 10 + SQLite, System.CommandLine, xUnit + FluentAssertions + NSubstitute

**Spec:** See `docs/superpowers/specs/2026-03-26-iaet-standalone-design.md` Section 4

**IMPORTANT:** All work on branch `phase2-stream-capture`. Create PR to main when complete.

---

## Phase 2 Scope

By the end of this phase:
- `ICdpSession` implemented as a real Playwright CDP wrapper
- `ICdpSession` expanded with `OnEvent` and `SendCommandAsync` so listeners can self-wire CDP events
- 5 protocol listeners: WebSocket, SSE, MediaStream (HLS/DASH), gRPC-Web, WebRTC signaling (WebAudio deferred to Phase 6 — requires Runtime domain hooking that is better done alongside the Explorer UI)
- `IStreamCatalog` implemented in SqliteCatalog (with `GetStreamByIdAsync`)
- `iaet streams list/show/frames` CLI commands
- `iaet capture start --capture-samples --capture-duration --capture-frames` flags
- No new EF migration needed — `CapturedStreamEntity` and its table were already created in Phase 1's `InitialCreate` migration
- Tests for all listeners, catalog operations, integration wiring, and CLI commands

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `src/Iaet.Capture/Cdp/PlaywrightCdpSession.cs` | Create | `ICdpSession` implementation wrapping Playwright's CDP access |
| `src/Iaet.Capture/Listeners/WebSocketListener.cs` | Create | Captures WebSocket connections and frame history |
| `src/Iaet.Capture/Listeners/SseListener.cs` | Create | Captures Server-Sent Events streams |
| `src/Iaet.Capture/Listeners/MediaStreamListener.cs` | Create | Detects HLS/DASH manifests, parses codec info |
| `src/Iaet.Capture/Listeners/GrpcWebListener.cs` | Create | Detects gRPC-Web and Protobuf payloads by content-type |
| `src/Iaet.Capture/Listeners/WebRtcListener.cs` | Create | Captures WebRTC signaling (SDP, ICE candidates) |
| `src/Iaet.Capture/StreamCaptureOptions.cs` | Create | Options for selective payload capture |
| `src/Iaet.Capture/PlaywrightCaptureSession.cs` | Modify | Integrate CDP session and protocol listeners |
| `src/Iaet.Capture/CaptureOptions.cs` | Modify | Add `CaptureSamples`, `SampleDurationSeconds`, `MaxFrames` |
| `src/Iaet.Capture/ServiceCollectionExtensions.cs` | Modify | Register protocol listeners |
| `src/Iaet.Catalog/SqliteStreamCatalog.cs` | Create | `IStreamCatalog` implementation |
| `src/Iaet.Catalog/ServiceCollectionExtensions.cs` | Modify | Register `IStreamCatalog` |
| `src/Iaet.Cli/Commands/StreamsCommand.cs` | Create | `iaet streams list/show/frames` commands |
| `src/Iaet.Cli/Commands/CaptureCommand.cs` | Modify | Add `--capture-samples` flag |
| `src/Iaet.Cli/Program.cs` | Modify | Register StreamsCommand |
| `tests/Iaet.Capture.Tests/Listeners/WebSocketListenerTests.cs` | Create | WebSocket frame capture tests |
| `tests/Iaet.Capture.Tests/Listeners/SseListenerTests.cs` | Create | SSE event capture tests |
| `tests/Iaet.Capture.Tests/Listeners/MediaStreamListenerTests.cs` | Create | HLS/DASH detection tests |
| `tests/Iaet.Capture.Tests/Listeners/GrpcWebListenerTests.cs` | Create | gRPC-Web detection tests |
| `tests/Iaet.Capture.Tests/Listeners/WebRtcListenerTests.cs` | Create | WebRTC signaling capture tests |
| `tests/Iaet.Catalog.Tests/SqliteStreamCatalogTests.cs` | Create | Stream persistence tests |

---

## Task 1: Create Branch, Expand ICdpSession, and Implement PlaywrightCdpSession

**Files:**
- Modify: `src/Iaet.Core/Abstractions/ICdpSession.cs`
- Create: `src/Iaet.Capture/Cdp/PlaywrightCdpSession.cs`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/prj/IAET
git checkout -b phase2-stream-capture
```

- [ ] **Step 2: Expand ICdpSession interface in Core**

The current `ICdpSession` only has `SubscribeToDomainAsync`/`UnsubscribeFromDomainAsync`, which is too narrow — listeners need to subscribe to CDP events and send commands. Expand it so `IProtocolListener` implementations can self-wire without depending on concrete types:

Modify `src/Iaet.Core/Abstractions/ICdpSession.cs`:

```csharp
using System.Text.Json;

namespace Iaet.Core.Abstractions;

/// <summary>
/// Abstraction over Chrome DevTools Protocol session.
/// Provides domain management, event subscription, and command execution.
/// </summary>
public interface ICdpSession
{
    Task SubscribeToDomainAsync(string domain, CancellationToken ct = default);
    Task UnsubscribeFromDomainAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to a CDP event. Returns a disposable that unsubscribes when disposed.
    /// </summary>
    IDisposable OnEvent(string eventName, Action<JsonElement> handler);

    /// <summary>
    /// Send a CDP command and return the result.
    /// </summary>
    Task<JsonElement> SendCommandAsync(string method, object? parameters = null, CancellationToken ct = default);
}
```

This allows listeners to fully self-wire in their `AttachAsync`:
```csharp
public async Task AttachAsync(ICdpSession cdp, IStreamCatalog catalog, CancellationToken ct)
{
    await cdp.SubscribeToDomainAsync("Network", ct);
    cdp.OnEvent("Network.webSocketCreated", data => { /* handle */ });
    cdp.OnEvent("Network.webSocketFrameReceived", data => { /* handle */ });
}
```

- [ ] **Step 3: Create PlaywrightCdpSession**

Create `src/Iaet.Capture/Cdp/PlaywrightCdpSession.cs`:

```csharp
using System.Text.Json;
using Iaet.Core.Abstractions;
using Microsoft.Playwright;

namespace Iaet.Capture.Cdp;

public sealed class PlaywrightCdpSession : ICdpSession, IAsyncDisposable
{
    private readonly ICDPSession _cdp;
    private readonly HashSet<string> _subscribedDomains = new(StringComparer.Ordinal);

    public PlaywrightCdpSession(ICDPSession cdp)
    {
        ArgumentNullException.ThrowIfNull(cdp);
        _cdp = cdp;
    }

    public static async Task<PlaywrightCdpSession> CreateAsync(IPage page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        var cdp = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        return new PlaywrightCdpSession(cdp);
    }

    public async Task SubscribeToDomainAsync(string domain, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (_subscribedDomains.Add(domain))
        {
            await _cdp.SendAsync($"{domain}.enable").ConfigureAwait(false);
        }
    }

    public async Task UnsubscribeFromDomainAsync(string domain, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (_subscribedDomains.Remove(domain))
        {
            await _cdp.SendAsync($"{domain}.disable").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Subscribe to a specific CDP event. Returns an action to unsubscribe.
    /// </summary>
    public Action OnEvent(string eventName, Action<JsonElement> handler)
    {
        _cdp.Event(eventName).OnEvent += (_, args) =>
        {
            handler(args);
        };
        // Note: Playwright CDP events don't support unsubscription directly
        // The session dispose handles cleanup
        return () => { };
    }

    /// <summary>
    /// Send a CDP command and return the result.
    /// </summary>
    public async Task<JsonElement> SendCommandAsync(string method, object? parameters = null, CancellationToken ct = default)
    {
        if (parameters is not null)
        {
            var json = JsonSerializer.Serialize(parameters);
            return await _cdp.SendAsync(method, JsonSerializer.Deserialize<JsonElement>(json)).ConfigureAwait(false);
        }
        return await _cdp.SendAsync(method).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var domain in _subscribedDomains.ToArray())
        {
            try
            {
                await _cdp.SendAsync($"{domain}.disable").ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Session may already be closed
            }
        }
        _subscribedDomains.Clear();
        await _cdp.DisposeAsync().ConfigureAwait(false);
    }
}
```

Note: The `ICdpSession` interface defined in Core has only `SubscribeToDomainAsync` and `UnsubscribeFromDomainAsync`. The `OnEvent` and `SendCommandAsync` methods are Capture-internal helpers that listeners use directly via the concrete `PlaywrightCdpSession` type (not through the interface). This is intentional — the Core interface stays minimal, and the Capture assembly has access to the full CDP capabilities.

- [ ] **Step 3: Verify build**

```bash
dotnet build src/Iaet.Capture
```

- [ ] **Step 4: Commit**

```bash
git add src/Iaet.Capture/Cdp/
git commit -m "feat: implement PlaywrightCdpSession wrapping Playwright's CDP access"
```

---

## Task 2: Extend CaptureOptions and StreamCaptureOptions

**Files:**
- Modify: `src/Iaet.Capture/CaptureOptions.cs`
- Create: `src/Iaet.Capture/StreamCaptureOptions.cs`

- [ ] **Step 1: Add stream capture options to CaptureOptions**

Modify `src/Iaet.Capture/CaptureOptions.cs`:

```csharp
namespace Iaet.Capture;

public sealed class CaptureOptions
{
    public required string TargetApplication { get; init; }
    public string? Profile { get; init; }
    public bool Headless { get; init; }
    public StreamCaptureOptions Streams { get; init; } = new();
}
```

- [ ] **Step 2: Create StreamCaptureOptions**

Create `src/Iaet.Capture/StreamCaptureOptions.cs`:

```csharp
namespace Iaet.Capture;

/// <summary>
/// Options controlling which protocol streams are captured and how much payload is stored.
/// </summary>
public sealed class StreamCaptureOptions
{
    /// <summary>Enable stream protocol detection and metadata capture.</summary>
    public bool Enabled { get; init; }

    /// <summary>Capture payload samples (WebSocket frames, HLS segments, RTP samples).</summary>
    public bool CaptureSamples { get; init; }

    /// <summary>Maximum WebSocket/SSE frames to store per connection. Default: 1000.</summary>
    public int MaxFramesPerConnection { get; init; } = 1000;

    /// <summary>Maximum duration for time-based sample capture (WebRTC). Default: 10 seconds.</summary>
    public int SampleDurationSeconds { get; init; } = 10;

    /// <summary>Maximum HLS/DASH segments to download per stream. Default: 3.</summary>
    public int MaxMediaSegments { get; init; } = 3;

    /// <summary>Directory for storing binary samples. Default: captures/{sessionId}/samples/</summary>
    public string? SampleOutputDirectory { get; init; }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/Iaet.Capture
```

- [ ] **Step 4: Commit**

```bash
git add src/Iaet.Capture/CaptureOptions.cs src/Iaet.Capture/StreamCaptureOptions.cs
git commit -m "feat: add StreamCaptureOptions for controlling protocol capture and payload sampling"
```

---

## Task 3: WebSocket Listener (TDD)

**Files:**
- Create: `src/Iaet.Capture/Listeners/WebSocketListener.cs`
- Create: `tests/Iaet.Capture.Tests/Listeners/WebSocketListenerTests.cs`

- [ ] **Step 1: Write tests first**

Create `tests/Iaet.Capture.Tests/Listeners/WebSocketListenerTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Capture.Listeners;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using NSubstitute;

namespace Iaet.Capture.Tests.Listeners;

public class WebSocketListenerTests
{
    [Fact]
    public void ProtocolName_IsWebSocket()
    {
        var listener = new WebSocketListener(new StreamCaptureOptions { Enabled = true });
        listener.ProtocolName.Should().Be("WebSocket");
        listener.Protocol.Should().Be(StreamProtocol.WebSocket);
    }

    [Fact]
    public void CanAttach_Always_ReturnsTrue()
    {
        var listener = new WebSocketListener(new StreamCaptureOptions { Enabled = true });
        var cdp = Substitute.For<ICdpSession>();
        listener.CanAttach(cdp).Should().BeTrue();
    }

    [Fact]
    public void ProcessWebSocketCreated_CreatesStream()
    {
        var options = new StreamCaptureOptions { Enabled = true, CaptureSamples = true, MaxFramesPerConnection = 100 };
        var listener = new WebSocketListener(options);
        var sessionId = Guid.NewGuid();

        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://api.example.com/ws");

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Url.Should().Be("wss://api.example.com/ws");
    }

    [Fact]
    public void ProcessWebSocketFrame_RecordsFrame()
    {
        var options = new StreamCaptureOptions { Enabled = true, CaptureSamples = true, MaxFramesPerConnection = 100 };
        var listener = new WebSocketListener(options);
        var sessionId = Guid.NewGuid();

        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://api.example.com/ws");
        listener.HandleWebSocketFrameReceived("req-1", "{\"type\":\"ping\"}", false);

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Frames.Should().ContainSingle()
            .Which.TextPayload.Should().Be("{\"type\":\"ping\"}");
    }

    [Fact]
    public void ProcessWebSocketFrame_RespectsMaxFrames()
    {
        var options = new StreamCaptureOptions { Enabled = true, CaptureSamples = true, MaxFramesPerConnection = 2 };
        var listener = new WebSocketListener(options);
        var sessionId = Guid.NewGuid();

        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");
        listener.HandleWebSocketFrameReceived("req-1", "frame1", false);
        listener.HandleWebSocketFrameReceived("req-1", "frame2", false);
        listener.HandleWebSocketFrameReceived("req-1", "frame3", false); // Should be dropped

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Frames.Should().HaveCount(2);
    }

    [Fact]
    public void ProcessWebSocketFrame_MetadataOnly_WhenSamplesDisabled()
    {
        var options = new StreamCaptureOptions { Enabled = true, CaptureSamples = false };
        var listener = new WebSocketListener(options);
        var sessionId = Guid.NewGuid();

        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");
        listener.HandleWebSocketFrameReceived("req-1", "{\"data\":1}", false);

        var streams = listener.GetPendingStreams();
        var stream = streams.Should().ContainSingle().Subject;
        stream.Frames.Should().BeNull(); // No frames stored in metadata-only mode
        stream.Metadata.Properties.Should().ContainKey("frameCount");
    }

    [Fact]
    public void ProcessWebSocketClosed_SetsEndedAt()
    {
        var options = new StreamCaptureOptions { Enabled = true };
        var listener = new WebSocketListener(options);
        var sessionId = Guid.NewGuid();

        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");
        listener.HandleWebSocketClosed("req-1");

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.EndedAt.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Add NSubstitute to Capture.Tests if not already there**

```bash
dotnet add tests/Iaet.Capture.Tests package NSubstitute
dotnet add tests/Iaet.Capture.Tests reference src/Iaet.Core
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/Iaet.Capture.Tests --filter "WebSocketListenerTests" -v n
```

Expected: FAIL — `WebSocketListener` does not exist.

- [ ] **Step 4: Implement WebSocketListener**

Create `src/Iaet.Capture/Listeners/WebSocketListener.cs`:

```csharp
using System.Collections.Concurrent;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class WebSocketListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, WebSocketState> _connections = new();

    public string ProtocolName => "WebSocket";
    public StreamProtocol Protocol => StreamProtocol.WebSocket;

    public WebSocketListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
        // CDP event wiring happens in PlaywrightCaptureSession which has access
        // to the concrete PlaywrightCdpSession.OnEvent method
    }

    public Task DetachAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public void HandleWebSocketCreated(Guid sessionId, string requestId, string url)
    {
        _connections[requestId] = new WebSocketState
        {
            SessionId = sessionId,
            Url = url,
            StartedAt = DateTimeOffset.UtcNow,
            Frames = _options.CaptureSamples ? [] : null,
            FrameCount = 0
        };
    }

    public void HandleWebSocketFrameReceived(string requestId, string payloadData, bool isBinary)
    {
        if (!_connections.TryGetValue(requestId, out var state)) return;

        state.FrameCount++;

        if (_options.CaptureSamples && state.Frames is not null
            && state.Frames.Count < _options.MaxFramesPerConnection)
        {
            state.Frames.Add(new StreamFrame
            {
                Timestamp = DateTimeOffset.UtcNow,
                Direction = StreamFrameDirection.Received,
                TextPayload = isBinary ? null : payloadData,
                BinaryPayload = isBinary ? Convert.FromBase64String(payloadData) : null,
                SizeBytes = payloadData.Length
            });
        }
    }

    public void HandleWebSocketFrameSent(string requestId, string payloadData, bool isBinary)
    {
        if (!_connections.TryGetValue(requestId, out var state)) return;

        state.FrameCount++;

        if (_options.CaptureSamples && state.Frames is not null
            && state.Frames.Count < _options.MaxFramesPerConnection)
        {
            state.Frames.Add(new StreamFrame
            {
                Timestamp = DateTimeOffset.UtcNow,
                Direction = StreamFrameDirection.Sent,
                TextPayload = isBinary ? null : payloadData,
                BinaryPayload = isBinary ? Convert.FromBase64String(payloadData) : null,
                SizeBytes = payloadData.Length
            });
        }
    }

    public void HandleWebSocketClosed(string requestId)
    {
        if (_connections.TryGetValue(requestId, out var state))
        {
            state.EndedAt = DateTimeOffset.UtcNow;
        }
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _connections.Values.Select(state => new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = state.SessionId,
            Protocol = StreamProtocol.WebSocket,
            Url = state.Url,
            StartedAt = state.StartedAt,
            EndedAt = state.EndedAt,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["frameCount"] = state.FrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }),
            Frames = state.Frames?.ToArray(),
            Tag = null
        }).ToList();
    }

    private sealed class WebSocketState
    {
        public Guid SessionId { get; set; }
        public required string Url { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public List<StreamFrame>? Frames { get; set; }
        public int FrameCount { get; set; }
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/Iaet.Capture.Tests --filter "WebSocketListenerTests" -v n
```

Expected: All 7 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Capture/Listeners/ tests/Iaet.Capture.Tests/Listeners/
git commit -m "feat: add WebSocketListener with frame capture and configurable limits"
```

---

## Task 4: SSE and MediaStream Listeners (TDD)

**Files:**
- Create: `src/Iaet.Capture/Listeners/SseListener.cs`
- Create: `src/Iaet.Capture/Listeners/MediaStreamListener.cs`
- Create: `tests/Iaet.Capture.Tests/Listeners/SseListenerTests.cs`
- Create: `tests/Iaet.Capture.Tests/Listeners/MediaStreamListenerTests.cs`

- [ ] **Step 1: Write SSE listener tests**

Create `tests/Iaet.Capture.Tests/Listeners/SseListenerTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Capture.Listeners;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using NSubstitute;

namespace Iaet.Capture.Tests.Listeners;

public class SseListenerTests
{
    [Fact]
    public void ProtocolName_IsSse()
    {
        var listener = new SseListener(new StreamCaptureOptions { Enabled = true });
        listener.ProtocolName.Should().Be("Server-Sent Events");
        listener.Protocol.Should().Be(StreamProtocol.ServerSentEvents);
    }

    [Fact]
    public void DetectSse_ByContentType()
    {
        var listener = new SseListener(new StreamCaptureOptions { Enabled = true });
        listener.IsServerSentEvents("text/event-stream").Should().BeTrue();
        listener.IsServerSentEvents("application/json").Should().BeFalse();
    }

    [Fact]
    public void HandleSseResponse_CreatesStream()
    {
        var listener = new SseListener(new StreamCaptureOptions { Enabled = true, CaptureSamples = true });
        var sessionId = Guid.NewGuid();

        listener.HandleSseDetected(sessionId, "https://api.example.com/events", "text/event-stream");

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Protocol.Should().Be(StreamProtocol.ServerSentEvents);
    }
}
```

- [ ] **Step 2: Write MediaStream listener tests**

Create `tests/Iaet.Capture.Tests/Listeners/MediaStreamListenerTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests.Listeners;

public class MediaStreamListenerTests
{
    [Theory]
    [InlineData("https://cdn.example.com/stream/master.m3u8", true)]
    [InlineData("https://cdn.example.com/stream/manifest.mpd", true)]
    [InlineData("https://cdn.example.com/api/data.json", false)]
    public void IsMediaManifest_DetectsCorrectly(string url, bool expected)
    {
        var listener = new MediaStreamListener(new StreamCaptureOptions { Enabled = true });
        listener.IsMediaManifest(url, null).Should().Be(expected);
    }

    [Theory]
    [InlineData("application/vnd.apple.mpegurl", true)]
    [InlineData("application/dash+xml", true)]
    [InlineData("application/json", false)]
    public void IsMediaManifest_DetectsByContentType(string contentType, bool expected)
    {
        var listener = new MediaStreamListener(new StreamCaptureOptions { Enabled = true });
        listener.IsMediaManifest("https://cdn.example.com/stream", contentType).Should().Be(expected);
    }

    [Fact]
    public void HandleManifestDetected_CreatesStream()
    {
        var listener = new MediaStreamListener(new StreamCaptureOptions { Enabled = true });
        var sessionId = Guid.NewGuid();

        listener.HandleManifestDetected(sessionId, "https://cdn.example.com/master.m3u8", "HLS");

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle().Which.Protocol.Should().Be(StreamProtocol.HlsStream);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/Iaet.Capture.Tests --filter "SseListenerTests|MediaStreamListenerTests" -v n
```

- [ ] **Step 4: Implement SseListener**

Create `src/Iaet.Capture/Listeners/SseListener.cs`:

```csharp
using System.Collections.Concurrent;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class SseListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, SseState> _connections = new();

    public string ProtocolName => "Server-Sent Events";
    public StreamProtocol Protocol => StreamProtocol.ServerSentEvents;

    public SseListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

    public bool IsServerSentEvents(string? contentType) =>
        contentType is not null &&
        contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

    public void HandleSseDetected(Guid sessionId, string url, string contentType)
    {
        _connections[url] = new SseState
        {
            SessionId = sessionId,
            Url = url,
            StartedAt = DateTimeOffset.UtcNow,
            ContentType = contentType,
            EventCount = 0,
            Frames = _options.CaptureSamples ? [] : null
        };
    }

    public void HandleSseEvent(string url, string eventType, string data)
    {
        if (!_connections.TryGetValue(url, out var state)) return;
        state.EventCount++;

        if (_options.CaptureSamples && state.Frames is not null
            && state.Frames.Count < _options.MaxFramesPerConnection)
        {
            state.Frames.Add(new StreamFrame
            {
                Timestamp = DateTimeOffset.UtcNow,
                Direction = StreamFrameDirection.Received,
                TextPayload = $"event: {eventType}\ndata: {data}",
                BinaryPayload = null,
                SizeBytes = data.Length
            });
        }
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _connections.Values.Select(s => new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = s.SessionId,
            Protocol = StreamProtocol.ServerSentEvents,
            Url = s.Url,
            StartedAt = s.StartedAt,
            EndedAt = null,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["contentType"] = s.ContentType,
                ["eventCount"] = s.EventCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }),
            Frames = null,
            Tag = null
        }).ToList();
    }

    private sealed class SseState
    {
        public Guid SessionId { get; set; }
        public required string Url { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public required string ContentType { get; set; }
        public int EventCount { get; set; }
        public List<StreamFrame>? Frames { get; set; }
    }
}
```

- [ ] **Step 5: Implement MediaStreamListener**

Create `src/Iaet.Capture/Listeners/MediaStreamListener.cs`:

```csharp
using System.Collections.Concurrent;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class MediaStreamListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, MediaState> _streams = new();

    public string ProtocolName => "Media Streams (HLS/DASH)";
    public StreamProtocol Protocol => StreamProtocol.HlsStream;

    public MediaStreamListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

    public bool IsMediaManifest(string url, string? contentType)
    {
        if (contentType is not null)
        {
            if (contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("dash+xml", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
               url.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase);
    }

    public void HandleManifestDetected(Guid sessionId, string url, string format)
    {
        var protocol = format.Equals("DASH", StringComparison.OrdinalIgnoreCase)
            ? StreamProtocol.DashStream
            : StreamProtocol.HlsStream;

        _streams.TryAdd(url, new MediaState
        {
            SessionId = sessionId,
            Url = url,
            Protocol = protocol,
            StartedAt = DateTimeOffset.UtcNow,
            Format = format
        });
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _streams.Values.Select(s => new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = s.SessionId,
            Protocol = s.Protocol,
            Url = s.Url,
            StartedAt = s.StartedAt,
            EndedAt = null,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["format"] = s.Format
            }),
            Frames = null,
            Tag = null
        }).ToList();
    }

    private sealed class MediaState
    {
        public Guid SessionId { get; set; }
        public required string Url { get; set; }
        public StreamProtocol Protocol { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public required string Format { get; set; }
    }
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet test tests/Iaet.Capture.Tests --filter "SseListenerTests|MediaStreamListenerTests" -v n
```

Expected: All 7 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Capture/Listeners/ tests/Iaet.Capture.Tests/Listeners/
git commit -m "feat: add SSE and MediaStream (HLS/DASH) listeners with detection and metadata capture"
```

---

## Task 5: gRPC-Web and WebRTC Listeners (TDD)

**Files:**
- Create: `src/Iaet.Capture/Listeners/GrpcWebListener.cs`
- Create: `src/Iaet.Capture/Listeners/WebRtcListener.cs`
- Create: `tests/Iaet.Capture.Tests/Listeners/GrpcWebListenerTests.cs`
- Create: `tests/Iaet.Capture.Tests/Listeners/WebRtcListenerTests.cs`

- [ ] **Step 1: Write gRPC-Web listener tests**

Create `tests/Iaet.Capture.Tests/Listeners/GrpcWebListenerTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests.Listeners;

public class GrpcWebListenerTests
{
    [Theory]
    [InlineData("application/grpc-web", true)]
    [InlineData("application/grpc-web+proto", true)]
    [InlineData("application/x-protobuf", true)]
    [InlineData("application/json", false)]
    public void IsGrpcOrProtobuf_DetectsCorrectly(string contentType, bool expected)
    {
        var listener = new GrpcWebListener(new StreamCaptureOptions { Enabled = true });
        listener.IsGrpcOrProtobuf(contentType).Should().Be(expected);
    }

    [Fact]
    public void HandleGrpcDetected_CreatesStream()
    {
        var listener = new GrpcWebListener(new StreamCaptureOptions { Enabled = true });
        var sessionId = Guid.NewGuid();

        listener.HandleGrpcDetected(sessionId, "https://api.example.com/grpc.Service/Method",
            "application/grpc-web", 128);

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Protocol.Should().Be(StreamProtocol.GrpcWeb);
    }
}
```

- [ ] **Step 2: Write WebRTC listener tests**

Create `tests/Iaet.Capture.Tests/Listeners/WebRtcListenerTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests.Listeners;

public class WebRtcListenerTests
{
    [Fact]
    public void ProtocolName_IsWebRtc()
    {
        var listener = new WebRtcListener(new StreamCaptureOptions { Enabled = true });
        listener.ProtocolName.Should().Be("WebRTC");
        listener.Protocol.Should().Be(StreamProtocol.WebRtc);
    }

    [Fact]
    public void HandlePeerConnectionCreated_CreatesStream()
    {
        var listener = new WebRtcListener(new StreamCaptureOptions { Enabled = true });
        var sessionId = Guid.NewGuid();

        listener.HandlePeerConnectionCreated(sessionId, "pc-1");

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Protocol.Should().Be(StreamProtocol.WebRtc);
    }

    [Fact]
    public void HandleSdpOffer_RecordsInMetadata()
    {
        var listener = new WebRtcListener(new StreamCaptureOptions { Enabled = true });
        var sessionId = Guid.NewGuid();

        listener.HandlePeerConnectionCreated(sessionId, "pc-1");
        listener.HandleSdpExchange("pc-1", "offer", "v=0\r\no=- 123 IN IP4 0.0.0.0\r\n");

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Metadata.Properties.Should().ContainKey("sdpOffer");
    }

    [Fact]
    public void HandleIceCandidate_RecordsCount()
    {
        var listener = new WebRtcListener(new StreamCaptureOptions { Enabled = true });
        var sessionId = Guid.NewGuid();

        listener.HandlePeerConnectionCreated(sessionId, "pc-1");
        listener.HandleIceCandidate("pc-1", "candidate:1 1 UDP 2130706431 192.168.1.1 5060 typ host");
        listener.HandleIceCandidate("pc-1", "candidate:2 1 UDP 1694498815 203.0.113.1 5060 typ srflx");

        var streams = listener.GetPendingStreams();
        streams.Should().ContainSingle()
            .Which.Metadata.Properties["iceCandidateCount"].Should().Be("2");
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/Iaet.Capture.Tests --filter "GrpcWebListenerTests|WebRtcListenerTests" -v n
```

- [ ] **Step 4: Implement GrpcWebListener**

Create `src/Iaet.Capture/Listeners/GrpcWebListener.cs`:

```csharp
using System.Collections.Concurrent;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class GrpcWebListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, GrpcState> _streams = new();

    public string ProtocolName => "gRPC-Web";
    public StreamProtocol Protocol => StreamProtocol.GrpcWeb;

    public GrpcWebListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

    public bool IsGrpcOrProtobuf(string? contentType) =>
        contentType is not null && (
            contentType.Contains("grpc-web", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("x-protobuf", StringComparison.OrdinalIgnoreCase));

    public void HandleGrpcDetected(Guid sessionId, string url, string contentType, long bodySize)
    {
        _streams.TryAdd(url, new GrpcState
        {
            SessionId = sessionId,
            Url = url,
            ContentType = contentType,
            BodySize = bodySize,
            DetectedAt = DateTimeOffset.UtcNow
        });
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _streams.Values.Select(s => new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = s.SessionId,
            Protocol = StreamProtocol.GrpcWeb,
            Url = s.Url,
            StartedAt = s.DetectedAt,
            EndedAt = null,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["contentType"] = s.ContentType,
                ["bodySize"] = s.BodySize.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }),
            Frames = null,
            Tag = null
        }).ToList();
    }

    private sealed class GrpcState
    {
        public Guid SessionId { get; set; }
        public required string Url { get; set; }
        public required string ContentType { get; set; }
        public long BodySize { get; set; }
        public DateTimeOffset DetectedAt { get; set; }
    }
}
```

- [ ] **Step 5: Implement WebRtcListener**

Create `src/Iaet.Capture/Listeners/WebRtcListener.cs`:

```csharp
using System.Collections.Concurrent;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class WebRtcListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, WebRtcState> _connections = new();

    public string ProtocolName => "WebRTC";
    public StreamProtocol Protocol => StreamProtocol.WebRtc;

    public WebRtcListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        // WebRTC events are captured via Runtime.evaluate hooking RTCPeerConnection
        await cdpSession.SubscribeToDomainAsync("Runtime", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

    public void HandlePeerConnectionCreated(Guid sessionId, string connectionId)
    {
        _connections[connectionId] = new WebRtcState
        {
            SessionId = sessionId,
            ConnectionId = connectionId,
            CreatedAt = DateTimeOffset.UtcNow,
            IceCandidateCount = 0
        };
    }

    public void HandleSdpExchange(string connectionId, string type, string sdp)
    {
        if (!_connections.TryGetValue(connectionId, out var state)) return;

        if (type.Equals("offer", StringComparison.OrdinalIgnoreCase))
            state.SdpOffer = sdp;
        else if (type.Equals("answer", StringComparison.OrdinalIgnoreCase))
            state.SdpAnswer = sdp;
    }

    public void HandleIceCandidate(string connectionId, string candidate)
    {
        if (_connections.TryGetValue(connectionId, out var state))
        {
            state.IceCandidateCount++;
        }
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _connections.Values.Select(s =>
        {
            var metadata = new Dictionary<string, string>
            {
                ["connectionId"] = s.ConnectionId,
                ["iceCandidateCount"] = s.IceCandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            if (s.SdpOffer is not null) metadata["sdpOffer"] = s.SdpOffer;
            if (s.SdpAnswer is not null) metadata["sdpAnswer"] = s.SdpAnswer;

            return new CapturedStream
            {
                Id = Guid.NewGuid(),
                SessionId = s.SessionId,
                Protocol = StreamProtocol.WebRtc,
                Url = $"webrtc://{s.ConnectionId}",
                StartedAt = s.CreatedAt,
                EndedAt = null,
                Metadata = new StreamMetadata(metadata),
                Frames = null,
                Tag = null
            };
        }).ToList();
    }

    private sealed class WebRtcState
    {
        public Guid SessionId { get; set; }
        public required string ConnectionId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? SdpOffer { get; set; }
        public string? SdpAnswer { get; set; }
        public int IceCandidateCount { get; set; }
    }
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet test tests/Iaet.Capture.Tests --filter "GrpcWebListenerTests|WebRtcListenerTests" -v n
```

Expected: All 6 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Capture/Listeners/ tests/Iaet.Capture.Tests/Listeners/
git commit -m "feat: add gRPC-Web and WebRTC listeners with protocol detection and signaling capture"
```

---

## Task 6: Implement IStreamCatalog in SqliteCatalog (TDD)

**Files:**
- Create: `src/Iaet.Catalog/SqliteStreamCatalog.cs`
- Modify: `src/Iaet.Catalog/ServiceCollectionExtensions.cs`
- Create: `tests/Iaet.Catalog.Tests/SqliteStreamCatalogTests.cs`

- [ ] **Step 1: Write stream catalog tests**

Create `tests/Iaet.Catalog.Tests/SqliteStreamCatalogTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog.Tests;

public class SqliteStreamCatalogTests : IDisposable
{
    private readonly CatalogDbContext _db;
    private readonly SqliteStreamCatalog _catalog;

    public SqliteStreamCatalogTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CatalogDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _catalog = new SqliteStreamCatalog(_db);

        // Seed a session
        _db.Sessions.Add(new Entities.CaptureSessionEntity
        {
            Id = _sessionId,
            Name = "test",
            TargetApplication = "Test",
            Profile = "default",
            StartedAt = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    private readonly Guid _sessionId = Guid.NewGuid();

    [Fact]
    public async Task SaveAndGet_RoundTrips()
    {
        var stream = new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Protocol = StreamProtocol.WebSocket,
            Url = "wss://example.com/ws",
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = new StreamMetadata(new Dictionary<string, string> { ["frameCount"] = "42" }),
            Frames = null,
            Tag = "chat"
        };

        await _catalog.SaveStreamAsync(stream);
        var result = await _catalog.GetStreamsBySessionAsync(_sessionId);

        result.Should().ContainSingle();
        result[0].Protocol.Should().Be(StreamProtocol.WebSocket);
        result[0].Url.Should().Be("wss://example.com/ws");
        result[0].Metadata.Properties["frameCount"].Should().Be("42");
        result[0].Tag.Should().Be("chat");
    }

    [Fact]
    public async Task GetStreamsBySession_ReturnsOnlyMatchingSession()
    {
        var otherSessionId = Guid.NewGuid();

        await _catalog.SaveStreamAsync(new CapturedStream
        {
            Id = Guid.NewGuid(), SessionId = _sessionId,
            Protocol = StreamProtocol.WebSocket, Url = "wss://a.com",
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = new StreamMetadata(new Dictionary<string, string>())
        });

        var result = await _catalog.GetStreamsBySessionAsync(otherSessionId);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveStream_WithFrames_PersistsFrameData()
    {
        var stream = new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            Protocol = StreamProtocol.WebSocket,
            Url = "wss://example.com/ws",
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = new StreamMetadata(new Dictionary<string, string>()),
            Frames = [
                new StreamFrame
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Direction = StreamFrameDirection.Received,
                    TextPayload = "{\"type\":\"ping\"}",
                    BinaryPayload = null,
                    SizeBytes = 15
                }
            ]
        };

        await _catalog.SaveStreamAsync(stream);
        var result = await _catalog.GetStreamsBySessionAsync(_sessionId);

        result.Should().ContainSingle()
            .Which.Frames.Should().ContainSingle()
            .Which.TextPayload.Should().Be("{\"type\":\"ping\"}");
    }

    public void Dispose() => _db.Dispose();
}
```

- [ ] **Step 2: Run tests to verify failure**

```bash
dotnet test tests/Iaet.Catalog.Tests --filter "SqliteStreamCatalogTests" -v n
```

Expected: FAIL — `SqliteStreamCatalog` does not exist.

- [ ] **Step 3: Implement SqliteStreamCatalog**

Create `src/Iaet.Catalog/SqliteStreamCatalog.cs`:

```csharp
using System.Text.Json;
using Iaet.Catalog.Entities;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog;

public sealed class SqliteStreamCatalog : IStreamCatalog
{
    private readonly CatalogDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SqliteStreamCatalog(CatalogDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task SaveStreamAsync(CapturedStream stream, CancellationToken ct = default)
    {
        _db.Streams.Add(new CapturedStreamEntity
        {
            Id = stream.Id,
            SessionId = stream.SessionId,
            Protocol = stream.Protocol.ToString(),
            Url = stream.Url,
            StartedAt = stream.StartedAt,
            EndedAt = stream.EndedAt,
            MetadataJson = JsonSerializer.Serialize(stream.Metadata.Properties, JsonOptions),
            FramesJson = stream.Frames is not null
                ? JsonSerializer.Serialize(stream.Frames, JsonOptions)
                : null,
            SamplePayloadPath = stream.SamplePayloadPath,
            Tag = stream.Tag
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CapturedStream>> GetStreamsBySessionAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        var entities = await _db.Streams
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(e => new CapturedStream
        {
            Id = e.Id,
            SessionId = e.SessionId,
            Protocol = Enum.Parse<StreamProtocol>(e.Protocol),
            Url = e.Url,
            StartedAt = e.StartedAt,
            EndedAt = e.EndedAt,
            Metadata = new StreamMetadata(
                e.MetadataJson is not null
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(e.MetadataJson, JsonOptions)!
                    : new Dictionary<string, string>()),
            Frames = e.FramesJson is not null
                ? JsonSerializer.Deserialize<StreamFrame[]>(e.FramesJson, JsonOptions)
                : null,
            SamplePayloadPath = e.SamplePayloadPath,
            Tag = e.Tag
        }).ToList();
    }
}
```

- [ ] **Step 3b: Expand IStreamCatalog in Core with GetStreamByIdAsync**

Modify `src/Iaet.Core/Abstractions/IStreamCatalog.cs`:
```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IStreamCatalog
{
    Task SaveStreamAsync(CapturedStream stream, CancellationToken ct = default);
    Task<IReadOnlyList<CapturedStream>> GetStreamsBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<CapturedStream?> GetStreamByIdAsync(Guid streamId, CancellationToken ct = default);
}
```

Add the `GetStreamByIdAsync` implementation to `SqliteStreamCatalog` — same query as `GetStreamsBySessionAsync` but filtered by `Id` and returning `FirstOrDefault`.

- [ ] **Step 4: Register IStreamCatalog in DI**

Modify `src/Iaet.Catalog/ServiceCollectionExtensions.cs` — add after the `IEndpointCatalog` registration:

```csharp
services.AddScoped<Iaet.Core.Abstractions.IStreamCatalog, SqliteStreamCatalog>();
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/Iaet.Catalog.Tests -v n
```

Expected: All catalog tests pass (existing + new stream tests).

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Catalog/ tests/Iaet.Catalog.Tests/
git commit -m "feat: implement IStreamCatalog with SQLite persistence for captured streams"
```

---

## Task 7: Integrate Listeners into PlaywrightCaptureSession

**Files:**
- Modify: `src/Iaet.Capture/PlaywrightCaptureSession.cs`
- Modify: `src/Iaet.Capture/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Update PlaywrightCaptureSession to create CDP session and wire listeners**

The key change: after creating the page, create a `PlaywrightCdpSession`, instantiate listeners based on `CaptureOptions.Streams`, attach them, and wire CDP events to listener handler methods.

Add to `PlaywrightCaptureSession`:
- A `PlaywrightCdpSession` field created in `StartAsync`
- A list of active `IProtocolListener` instances
- Response event handler that checks for SSE, gRPC-Web, HLS/DASH content types
- CDP event subscriptions for WebSocket and WebRTC events
- A `GetCapturedStreamsAsync()` method (or drain via listeners) to collect stream data
- Integration with `IStreamCatalog` for persistence

The session receives an optional `IStreamCatalog` and list of `IProtocolListener` via constructor (or from the factory).

- [ ] **Step 2: Update PlaywrightCaptureSession constructor and factory**

The session now accepts an optional `IStreamCatalog` and list of listeners. When stream capture is enabled, `StartAsync` creates a `PlaywrightCdpSession` and calls `AttachAsync` on each listener. `StopAsync` drains streams from listeners to the catalog.

Key changes to `PlaywrightCaptureSession`:
```csharp
// New fields
private readonly IStreamCatalog? _streamCatalog;
private readonly IReadOnlyList<IProtocolListener> _listeners;
private PlaywrightCdpSession? _cdpSession;

// Updated constructor
public PlaywrightCaptureSession(CaptureOptions options,
    IStreamCatalog? streamCatalog = null,
    IReadOnlyList<IProtocolListener>? listeners = null)
{
    _options = options;
    _streamCatalog = streamCatalog;
    _listeners = listeners ?? [];
}

// In StartAsync, after page creation:
if (_options.Streams.Enabled && _listeners.Count > 0)
{
    _cdpSession = await PlaywrightCdpSession.CreateAsync(_page, ct);
    foreach (var listener in _listeners.Where(l => l.CanAttach(_cdpSession)))
    {
        await listener.AttachAsync(_cdpSession, _streamCatalog!, ct);
    }
}

// In StopAsync, before closing browser — drain and persist streams:
if (_streamCatalog is not null)
{
    foreach (var listener in _listeners)
    {
        // Each listener has a GetPendingStreams() method
        // Persist via IStreamCatalog
        await listener.DetachAsync(ct);
    }
}
if (_cdpSession is not null) await _cdpSession.DisposeAsync();
```

Update `ICaptureSessionFactory`:
```csharp
public interface ICaptureSessionFactory
{
    ICaptureSession Create(CaptureOptions options);
    ICaptureSession Create(CaptureOptions options, IStreamCatalog? streamCatalog,
        IReadOnlyList<IProtocolListener>? listeners);
}
```

Update `PlaywrightCaptureSessionFactory` to implement both overloads.

- [ ] **Step 3: Write integration tests**

Create `tests/Iaet.Capture.Tests/PlaywrightCaptureSessionIntegrationTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Capture;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using NSubstitute;

namespace Iaet.Capture.Tests;

public class CaptureSessionStreamIntegrationTests
{
    [Fact]
    public void Create_WithStreamOptions_AcceptsListeners()
    {
        var options = new CaptureOptions
        {
            TargetApplication = "Test",
            Streams = new StreamCaptureOptions { Enabled = true }
        };
        var catalog = Substitute.For<IStreamCatalog>();
        var listener = Substitute.For<IProtocolListener>();
        listener.CanAttach(Arg.Any<ICdpSession>()).Returns(true);

        var factory = new PlaywrightCaptureSessionFactory();
        var session = factory.Create(options, catalog, [listener]);

        session.Should().NotBeNull();
        session.TargetApplication.Should().Be("Test");
    }

    [Fact]
    public void Create_WithoutStreamOptions_WorksAsBeforeWith()
    {
        var options = new CaptureOptions { TargetApplication = "Test" };
        var factory = new PlaywrightCaptureSessionFactory();
        var session = factory.Create(options);

        session.Should().NotBeNull();
    }
}
```

These test the factory creates sessions correctly. Full CDP integration testing requires a live browser and is manual.

- [ ] **Step 4: Update ServiceCollectionExtensions to register all listeners**

```csharp
public static IServiceCollection AddIaetCapture(this IServiceCollection services)
{
    services.AddSingleton<ICaptureSessionFactory, PlaywrightCaptureSessionFactory>();
    services.AddTransient<WebSocketListener>();
    services.AddTransient<SseListener>();
    services.AddTransient<MediaStreamListener>();
    services.AddTransient<GrpcWebListener>();
    services.AddTransient<WebRtcListener>();
    return services;
}
```

- [ ] **Step 4: Build and run all tests**

```bash
dotnet build Iaet.slnx
dotnet test Iaet.slnx -v n
```

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Capture/
git commit -m "feat: integrate protocol listeners into PlaywrightCaptureSession with CDP event wiring"
```

---

## Task 8: Add Streams CLI Commands

**Files:**
- Create: `src/Iaet.Cli/Commands/StreamsCommand.cs`
- Modify: `src/Iaet.Cli/Commands/CaptureCommand.cs`
- Modify: `src/Iaet.Cli/Program.cs`

- [ ] **Step 1: Create StreamsCommand**

Create `src/Iaet.Cli/Commands/StreamsCommand.cs`:

Commands:
- `iaet streams list --session-id <guid>` — lists all streams for a session (protocol, URL, started, frame count)
- `iaet streams show --stream-id <guid>` — shows full metadata for a stream
- `iaet streams frames --stream-id <guid>` — shows frame history (if captured)

Each command creates a DI scope, runs MigrateAsync, resolves IStreamCatalog.

- [ ] **Step 2: Add stream capture flags to CaptureCommand**

Add options:
- `--capture-streams` (bool, default true) — enable stream protocol detection
- `--capture-samples` (bool) — enable stream payload capture
- `--capture-duration <int>` (default 10) — max seconds for time-based sample capture
- `--capture-frames <int>` (default 1000) — max frames per connection

Wire all into `CaptureOptions.Streams`:
```csharp
Streams = new StreamCaptureOptions
{
    Enabled = captureStreams,
    CaptureSamples = captureSamples,
    SampleDurationSeconds = captureDuration,
    MaxFramesPerConnection = captureFrames
}
```

Also update the `CaptureCommand` to resolve `IStreamCatalog` from DI and pass it to the factory along with resolved listeners when stream capture is enabled.

- [ ] **Step 3: Register StreamsCommand in Program.cs**

Add to the root command:
```csharp
StreamsCommand.Create(host.Services)
```

- [ ] **Step 4: Verify CLI**

```bash
dotnet run --project src/Iaet.Cli -- streams --help
dotnet run --project src/Iaet.Cli -- capture start --help
```

Expected: streams commands appear in help, capture shows --capture-samples flag.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cli/
git commit -m "feat: add 'iaet streams' CLI commands and --capture-samples flag"
```

---

## Task 9: Update Documentation and Create PR

**Files:**
- Modify: `README.md`
- Modify: `src/Iaet.Capture/README.md`

- [ ] **Step 1: Update README.md**

Add to Features list: stream capture is now implemented (not "coming").
Update CLI reference to include `streams` commands.
Add section on stream capture usage:
```bash
# Capture with stream protocol detection
iaet capture start --target "Spotify" --url https://open.spotify.com --session spotify-001

# Capture with payload samples
iaet capture start --target "Spotify" --url https://open.spotify.com --session spotify-002 --capture-samples

# View discovered streams
iaet streams list --session-id <id>

# View stream details
iaet streams show --stream-id <id>
```

- [ ] **Step 2: Update Iaet.Capture README**

Add section on protocol listeners, StreamCaptureOptions, and how to implement custom IProtocolListener.

- [ ] **Step 3: Run full test suite**

```bash
dotnet test Iaet.slnx -c Release
```

Expected: All tests pass.

- [ ] **Step 4: Commit and push**

```bash
git add README.md src/Iaet.Capture/README.md
git commit -m "docs: update README with stream capture documentation"
git push origin phase2-stream-capture
```

- [ ] **Step 5: Create PR**

```bash
gh pr create --title "Phase 2: Stream Capture — WebSocket, SSE, WebRTC, HLS/DASH, gRPC-Web" --body "$(cat <<'EOF'
## Summary

Adds first-class data stream capture to IAET alongside existing HTTP request capture:

- **ICdpSession implementation** — real Playwright CDP wrapper for protocol listeners
- **5 protocol listeners** — WebSocket (frame history), SSE (event detection), MediaStream (HLS/DASH manifest parsing), gRPC-Web (Protobuf detection), WebRTC (SDP/ICE signaling capture)
- **IStreamCatalog** — SQLite persistence for captured streams with frame data
- **Selective payload capture** — `--capture-samples` flag with configurable limits (max frames, sample duration, max segments)
- **CLI commands** — `iaet streams list/show/frames`
- **Extensible** — custom `IProtocolListener` implementations can be registered via DI

### New/Modified Files
- `src/Iaet.Capture/Cdp/PlaywrightCdpSession.cs` — CDP session wrapper
- `src/Iaet.Capture/Listeners/*.cs` — 5 protocol listeners
- `src/Iaet.Capture/StreamCaptureOptions.cs` — capture configuration
- `src/Iaet.Catalog/SqliteStreamCatalog.cs` — stream persistence
- `src/Iaet.Cli/Commands/StreamsCommand.cs` — CLI commands

### Test Plan
- [ ] WebSocket listener: frame capture, max frame limits, metadata-only mode, connection close
- [ ] SSE listener: content-type detection, stream creation
- [ ] MediaStream listener: HLS/DASH URL and content-type detection
- [ ] gRPC-Web listener: content-type detection
- [ ] WebRTC listener: peer connection, SDP exchange, ICE candidates
- [ ] Stream catalog: save/get round-trip, session filtering, frame persistence
- [ ] CLI: `streams --help`, `capture start --capture-samples`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## What's Next

After Phase 2, IAET has:
- HTTP request capture (existing)
- WebSocket, SSE, WebRTC, HLS/DASH, gRPC-Web stream capture (new)
- SQLite persistence for both requests and streams
- CLI commands for browsing streams

**Phase 3 (Schema + Replay)** is the next implementation phase.
