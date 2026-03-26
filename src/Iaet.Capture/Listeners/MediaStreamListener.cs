using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class MediaStreamListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, MediaState> _streams = new();

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
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

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
