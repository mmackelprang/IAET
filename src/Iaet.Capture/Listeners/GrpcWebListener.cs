using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Capture.Listeners;

public sealed class GrpcWebListener : IProtocolListener
{
    private readonly StreamCaptureOptions _options;
    private readonly ConcurrentDictionary<string, GrpcState> _streams = new();
    private readonly List<IDisposable> _subscriptions = [];
    private Guid _sessionId;

    public GrpcWebListener(StreamCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string ProtocolName => "gRPC-Web";

    public StreamProtocol Protocol => StreamProtocol.GrpcWeb;

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
            long bodySize = 0;

            if (data.TryGetProperty("response", out var resp))
            {
                if (resp.TryGetProperty("url", out var urlEl)) url = urlEl.GetString();
                if (resp.TryGetProperty("headers", out var headers))
                {
                    if (headers.TryGetProperty("content-type", out var ct1)) contentType = ct1.GetString();
                    else if (headers.TryGetProperty("Content-Type", out var ct2)) contentType = ct2.GetString();
                }
                if (resp.TryGetProperty("encodedDataLength", out var bdEl)) bodySize = bdEl.GetInt64();
            }

            if (url is null || contentType is null) return;
            if (!IsGrpcOrProtobuf(contentType)) return;

            HandleGrpcDetected(_sessionId, url, contentType, bodySize);
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

    public static bool IsGrpcOrProtobuf(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        return contentType.Contains("grpc-web", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("x-protobuf", StringComparison.OrdinalIgnoreCase);
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "CDP delivers URLs as strings")]
    public void HandleGrpcDetected(Guid sessionId, string url, string contentType, long bodySize)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        var state = new GrpcState(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            Url: url,
            ContentType: contentType,
            BodySize: bodySize,
            StartedAt: DateTimeOffset.UtcNow);
        _streams.TryAdd(url, state);
    }

    public IReadOnlyList<CapturedStream> GetPendingStreams()
    {
        return _streams.Values.Select(s => new CapturedStream
        {
            Id = s.Id,
            SessionId = s.SessionId,
            Protocol = StreamProtocol.GrpcWeb,
            Url = s.Url,
            StartedAt = s.StartedAt,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["contentType"] = s.ContentType,
                ["bodySize"] = s.BodySize.ToString(CultureInfo.InvariantCulture),
            }),
        }).ToList();
    }

    private sealed record GrpcState(
        Guid Id,
        Guid SessionId,
        string Url,
        string ContentType,
        long BodySize,
        DateTimeOffset StartedAt);
}
