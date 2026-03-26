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
        await cdpSession.SubscribeToDomainAsync("Network", ct).ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken ct = default) => Task.CompletedTask;

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
