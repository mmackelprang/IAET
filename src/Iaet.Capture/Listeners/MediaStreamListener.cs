using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class MediaStreamListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, MediaState> _streams = new();
    private readonly List<IDisposable> _subscriptions = [];
    private Guid _sessionId;

    public MediaStreamListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string ProtocolName => "MediaStream";

    public StreamProtocol Protocol => StreamProtocol.HlsStream;

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

            if (url is null) return;
            if (!IsMediaManifest(url, contentType)) return;

            var format = (contentType is not null && contentType.Contains("dash+xml", StringComparison.OrdinalIgnoreCase))
                || url.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase)
                ? "DASH"
                : "HLS";

            HandleManifestDetected(_sessionId, url, format);
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

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "CDP delivers URLs as strings; detection method must accept strings")]
    public static bool IsMediaManifest(string url, string? contentType)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (contentType is not null)
        {
            if (contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("dash+xml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "CDP delivers URLs as strings")]
    public void HandleManifestDetected(Guid sessionId, string url, string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var protocol = format.Equals("DASH", StringComparison.OrdinalIgnoreCase)
            ? StreamProtocol.DashStream
            : StreamProtocol.HlsStream;

        var state = new MediaState(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            Url: url,
            Format: format,
            Protocol: protocol,
            StartedAt: DateTimeOffset.UtcNow);
        _streams.TryAdd(url, state);
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _streams.Values.Select(s => new CapturedStream
        {
            Id = s.Id,
            SessionId = s.SessionId,
            Protocol = s.Protocol,
            Url = s.Url,
            StartedAt = s.StartedAt,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["format"] = s.Format,
            }),
        }).ToList();
    }

    private sealed record MediaState(
        Guid Id,
        Guid SessionId,
        string Url,
        string Format,
        StreamProtocol Protocol,
        DateTimeOffset StartedAt);
}
