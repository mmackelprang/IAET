using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class WebSocketListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, WebSocketState> _connections = new();

    public WebSocketListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string ProtocolName => "WebSocket";

    public StreamProtocol Protocol => StreamProtocol.WebSocket;

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cdpSession);
        ArgumentNullException.ThrowIfNull(catalog);
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "CDP delivers URLs as strings; callers should not need to construct Uri objects")]
    public void HandleWebSocketCreated(Guid sessionId, string requestId, string url)
    {
        var state = new WebSocketState(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            Url: url,
            StartedAt: DateTimeOffset.UtcNow,
            Frames: _options.CaptureSamples ? [] : null);
        _connections[requestId] = state;
    }

    public void HandleWebSocketFrameReceived(string requestId, string payloadData, bool isBinary)
    {
        ArgumentNullException.ThrowIfNull(payloadData);
        RecordFrame(requestId, payloadData, isBinary, StreamFrameDirection.Received);
    }

    public void HandleWebSocketFrameSent(string requestId, string payloadData, bool isBinary)
    {
        ArgumentNullException.ThrowIfNull(payloadData);
        RecordFrame(requestId, payloadData, isBinary, StreamFrameDirection.Sent);
    }

    public void HandleWebSocketClosed(string requestId)
    {
        if (_connections.TryGetValue(requestId, out var state))
        {
            _connections[requestId] = state with { EndedAt = DateTimeOffset.UtcNow };
        }
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _connections.Values.Select(s => new CapturedStream
        {
            Id = s.Id,
            SessionId = s.SessionId,
            Protocol = StreamProtocol.WebSocket,
            Url = s.Url,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["frameCount"] = s.FrameCount.ToString(CultureInfo.InvariantCulture),
            }),
            Frames = s.Frames is not null ? s.Frames.AsReadOnly() : null,
        }).ToList();
    }

    private void RecordFrame(string requestId, string payloadData, bool isBinary, StreamFrameDirection direction)
    {
        if (!_connections.TryGetValue(requestId, out var state))
        {
            return;
        }

        var newCount = state.FrameCount + 1;
        var updatedState = state with { FrameCount = newCount };

        if (_options.CaptureSamples && state.Frames is not null && state.FrameCount < _options.MaxFramesPerConnection)
        {
            var frame = new StreamFrame
            {
                Timestamp = DateTimeOffset.UtcNow,
                Direction = direction,
                TextPayload = isBinary ? null : payloadData,
                BinaryPayload = isBinary ? Encoding.UTF8.GetBytes(payloadData) : null,
                SizeBytes = payloadData.Length,
            };
            state.Frames.Add(frame);
        }

        _connections[requestId] = updatedState;
    }

    private sealed record WebSocketState(
        Guid Id,
        Guid SessionId,
        string Url,
        DateTimeOffset StartedAt,
        List<StreamFrame>? Frames)
    {
        public DateTimeOffset? EndedAt { get; init; }
        public int FrameCount { get; init; }
    }
}
