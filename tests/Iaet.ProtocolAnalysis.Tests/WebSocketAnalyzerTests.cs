using FluentAssertions;
using Iaet.Core.Models;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class WebSocketAnalyzerTests
{
    [Fact]
    public void Analyze_classifies_json_messages()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("""{"type":"connection_init"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"connection_ack"}""", StreamFrameDirection.Received),
            MakeFrame("""{"type":"data","payload":{"user":"test"}}""", StreamFrameDirection.Received),
        ]);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().Contain(["connection_init", "connection_ack", "data"]);
    }

    [Fact]
    public void Analyze_detects_graphql_ws_subprotocol()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("""{"type":"connection_init"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"connection_ack"}""", StreamFrameDirection.Received),
            MakeFrame("""{"type":"subscribe","payload":{"query":"{ users { id } }"}}""", StreamFrameDirection.Sent),
        ],
        subprotocol: "graphql-ws");

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.SubProtocol.Should().Be("graphql-ws");
        result.Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_detects_heartbeat_patterns()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("""{"type":"ping"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"pong"}""", StreamFrameDirection.Received),
            MakeFrame("""{"type":"ping"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"pong"}""", StreamFrameDirection.Received),
        ]);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().Contain(["ping", "pong"]);
        result.HasHeartbeat.Should().BeTrue();
    }

    [Fact]
    public void Analyze_handles_non_json_frames()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("plain text message", StreamFrameDirection.Sent),
        ]);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().Contain("text");
    }

    [Fact]
    public void Analyze_handles_empty_frames()
    {
        var stream = MakeStream(StreamProtocol.WebSocket, []);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().BeEmpty();
    }

    [Fact]
    public void CanAnalyze_returns_true_for_websocket()
    {
        var analyzer = new WebSocketAnalyzer();
        analyzer.CanAnalyze(StreamProtocol.WebSocket).Should().BeTrue();
        analyzer.CanAnalyze(StreamProtocol.ServerSentEvents).Should().BeFalse();
    }

    private static CapturedStream MakeStream(StreamProtocol protocol, IReadOnlyList<StreamFrame> frames, string? subprotocol = null)
    {
        var metadata = new Dictionary<string, string>();
        if (subprotocol is not null)
            metadata["subprotocol"] = subprotocol;

        return new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Protocol = protocol,
            Url = "wss://example.com/ws",
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = new StreamMetadata(metadata),
            Frames = frames,
        };
    }

    private static StreamFrame MakeFrame(string text, StreamFrameDirection direction) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Direction = direction,
        TextPayload = text,
        SizeBytes = text.Length,
    };
}
