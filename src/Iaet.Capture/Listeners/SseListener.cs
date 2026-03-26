using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class SseListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, SseState> _streams = new();
    private readonly List<IDisposable> _subscriptions = [];
    private Guid _sessionId;

    public SseListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string ProtocolName => "ServerSentEvents";

    public StreamProtocol Protocol => StreamProtocol.ServerSentEvents;

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cdpSession);
        ArgumentNullException.ThrowIfNull(catalog);
        _sessionId = Guid.NewGuid();
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);

        _subscriptions.Add(cdpSession.OnEvent("Network.responseReceived", data =>
        {
            string? url = null;
            string? contentType = null;

            if (data.TryGetProperty("response", out var resp))
            {
                if (resp.TryGetProperty("url", out var urlEl)) url = urlEl.GetString();
                if (resp.TryGetProperty("headers", out var headers))
                {
                    if (headers.TryGetProperty("content-type", out var ct1)) contentType = ct1.GetString();
                    else if (headers.TryGetProperty("Content-Type", out var ct2)) contentType = ct2.GetString();
                }
            }

            if (url is null || contentType is null) return;
            if (IsServerSentEvents(contentType))
            {
                HandleSseDetected(_sessionId, url, contentType);
            }
        }));

        _subscriptions.Add(cdpSession.OnEvent("Network.dataReceived", data =>
        {
            // dataReceived provides raw chunk data; SSE frames are text lines
            // We record a generic "data" event when we have a matching SSE stream
            if (!data.TryGetProperty("requestId", out var reqIdEl)) return;
            var requestId = reqIdEl.GetString();
            if (requestId is null) return;

            // Map requestId to URL: find matching SSE stream by requestId tracking
            // DataReceived does not carry URL; we store a requestId→url index if available
            // For now, use defensive approach: skip if no URL mapping exists
            // (Full requestId→url tracking would require Network.requestWillBeSent subscription)
        }));
    }

    public Task DetachAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    public static bool IsServerSentEvents(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        return contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase);
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "CDP delivers URLs as strings")]
    public void HandleSseDetected(Guid sessionId, string url, string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        var state = new SseState(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            Url: url,
            ContentType: contentType,
            StartedAt: DateTimeOffset.UtcNow,
            Frames: _options.CaptureSamples ? [] : null);
        _streams.TryAdd(url, state);
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "CDP delivers URLs as strings")]
    public void HandleSseEvent(string url, string eventType, string data)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(data);

        if (!_streams.TryGetValue(url, out var state))
        {
            return;
        }

        var newCount = state.EventCount + 1;
        var updatedState = state with { EventCount = newCount };

        if (_options.CaptureSamples && state.Frames is not null
            && state.Frames.Count < _options.MaxFramesPerConnection)
        {
            var payload = $"event: {eventType}\ndata: {data}";
            var frame = new StreamFrame
            {
                Timestamp = DateTimeOffset.UtcNow,
                Direction = StreamFrameDirection.Received,
                TextPayload = payload,
                SizeBytes = payload.Length,
            };
            state.Frames.Add(frame);
        }

        _streams[url] = updatedState;
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _streams.Values.Select(s => new CapturedStream
        {
            Id = s.Id,
            SessionId = s.SessionId,
            Protocol = StreamProtocol.ServerSentEvents,
            Url = s.Url,
            StartedAt = s.StartedAt,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["contentType"] = s.ContentType,
                ["eventCount"] = s.EventCount.ToString(CultureInfo.InvariantCulture),
            }),
            Frames = s.Frames is not null ? s.Frames.AsReadOnly() : null,
        }).ToList();
    }

    private sealed record SseState(
        Guid Id,
        Guid SessionId,
        string Url,
        string ContentType,
        DateTimeOffset StartedAt,
        List<StreamFrame>? Frames)
    {
        public int EventCount { get; init; }
    }
}
