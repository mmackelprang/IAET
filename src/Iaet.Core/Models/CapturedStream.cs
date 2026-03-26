using System.Diagnostics.CodeAnalysis;

namespace Iaet.Core.Models;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "CapturedStream is a domain model, not a System.IO.Stream")]
public sealed record CapturedStream
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required StreamProtocol Protocol { get; init; }
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public required StreamMetadata Metadata { get; init; }
    public IReadOnlyList<StreamFrame>? Frames { get; init; }
    public string? SamplePayloadPath { get; init; }
    public string? Tag { get; init; }
}

public enum StreamProtocol
{
    WebSocket,
    ServerSentEvents,
    WebRtc,
    HlsStream,
    DashStream,
    GrpcWeb,
    WebAudio,
    Unknown
}

public sealed record StreamMetadata(
    Dictionary<string, string> Properties
);

public sealed record StreamFrame
{
    public required DateTimeOffset Timestamp { get; init; }
    public required StreamFrameDirection Direction { get; init; }
    public string? TextPayload { get; init; }
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Binary frame payload; array is appropriate for raw bytes")]
    public byte[]? BinaryPayload { get; init; }
    public required long SizeBytes { get; init; }
}

public enum StreamFrameDirection { Sent, Received }
