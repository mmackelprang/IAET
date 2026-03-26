using System.Collections.Concurrent;
using System.Globalization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class WebRtcListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, WebRtcState> _connections = new();
    private readonly List<IDisposable> _subscriptions = [];
    private Guid _sessionId;

    public WebRtcListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string ProtocolName => "WebRTC";

    public StreamProtocol Protocol => StreamProtocol.WebRtc;

    public bool CanAttach(ICdpSession cdpSession) => true;

    public async Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cdpSession);
        ArgumentNullException.ThrowIfNull(catalog);
        _sessionId = Guid.NewGuid();

        // TODO (Phase 3): WebRTC CDP domain events vary significantly between Chrome versions.
        // The WebRTC domain may expose peerConnectionCreated / addedICECandidate in some versions.
        // In others, hooking RTCPeerConnection requires Runtime domain JS injection.
        // For now we enable the domain to receive whatever events Chrome exposes, and
        // subscribe defensively — unknown events are silently ignored.
        await cdpSession.SubscribeToDomainAsync("WebRTC", ct).ConfigureAwait(false);

        _subscriptions.Add(cdpSession.OnEvent("WebRTC.peerConnectionCreated", data =>
        {
            string? connectionId = null;
            if (data.TryGetProperty("peerConnectionId", out var pcId)) connectionId = pcId.GetString();
            if (connectionId is null) return;
            HandlePeerConnectionCreated(_sessionId, connectionId);
        }));

        _subscriptions.Add(cdpSession.OnEvent("WebRTC.addedICECandidate", data =>
        {
            string? connectionId = null;
            string? candidate = null;
            if (data.TryGetProperty("peerConnectionId", out var pcId)) connectionId = pcId.GetString();
            if (data.TryGetProperty("candidate", out var candEl))
            {
                if (candEl.TryGetProperty("candidate", out var c)) candidate = c.GetString();
                else candidate = candEl.GetString();
            }
            if (connectionId is null || candidate is null) return;
            HandleIceCandidate(connectionId, candidate);
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

    public void HandlePeerConnectionCreated(Guid sessionId, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        var state = new WebRtcState(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            ConnectionId: connectionId,
            StartedAt: DateTimeOffset.UtcNow);
        _connections.TryAdd(connectionId, state);
    }

    public void HandleSdpExchange(string connectionId, string type, string sdp)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(sdp);

        if (!_connections.TryGetValue(connectionId, out var state))
        {
            return;
        }

        var sdpKey = $"sdp.{type}";
        var updatedSdp = new Dictionary<string, string>(state.SdpEntries) { [sdpKey] = sdp };
        _connections[connectionId] = state with { SdpEntries = updatedSdp };
    }

    public void HandleIceCandidate(string connectionId, string candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!_connections.TryGetValue(connectionId, out var state))
        {
            return;
        }

        _connections[connectionId] = state with { IceCandidateCount = state.IceCandidateCount + 1 };
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _connections.Values.Select(s =>
        {
            var props = new Dictionary<string, string>
            {
                ["iceCandidateCount"] = s.IceCandidateCount.ToString(CultureInfo.InvariantCulture),
            };
            foreach (var (key, value) in s.SdpEntries)
            {
                props[key] = value;
            }

            return new CapturedStream
            {
                Id = s.Id,
                SessionId = s.SessionId,
                Protocol = StreamProtocol.WebRtc,
                Url = $"webrtc://{s.ConnectionId}",
                StartedAt = s.StartedAt,
                Metadata = new StreamMetadata(props),
            };
        }).ToList();
    }

    private sealed record WebRtcState(
        Guid Id,
        Guid SessionId,
        string ConnectionId,
        DateTimeOffset StartedAt)
    {
        public int IceCandidateCount { get; init; }
        public Dictionary<string, string> SdpEntries { get; init; } = [];
    }
}
