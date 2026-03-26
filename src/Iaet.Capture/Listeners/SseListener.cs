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
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

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

        if (_options.CaptureSamples && state.Frames is not null)
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
