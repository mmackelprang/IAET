using FluentAssertions;
using Iaet.Capture;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;
using NSubstitute;
using Iaet.Core.Abstractions;

namespace Iaet.Capture.Tests.Listeners;

public class WebSocketListenerTests
{
    private static WebSocketListener CreateListener(bool captureSamples = true, int maxFrames = 1000)
    {
        var options = new StreamCaptureOptions
        {
            Enabled = true,
            CaptureSamples = captureSamples,
            MaxFramesPerConnection = maxFrames,
        };
        return new WebSocketListener(options);
    }

    [Fact]
    public void ProtocolName_IsWebSocket()
    {
        var listener = CreateListener();
        listener.ProtocolName.Should().Be("WebSocket");
    }

    [Fact]
    public void CanAttach_ReturnsTrue()
    {
        var listener = CreateListener();
        var session = Substitute.For<ICdpSession>();
        listener.CanAttach(session).Should().BeTrue();
    }

    [Fact]
    public void HandleWebSocketCreated_CreatesStream()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");

        var streams = listener.GetPendingStreams();
        streams.Should().HaveCount(1);
        streams[0].Url.Should().Be("wss://example.com/ws");
        streams[0].Protocol.Should().Be(StreamProtocol.WebSocket);
        streams[0].SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void HandleWebSocketFrameReceived_RecordsFrame_WhenCaptureSamplesEnabled()
    {
        var listener = CreateListener(captureSamples: true);
        var sessionId = Guid.NewGuid();
        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");
        listener.HandleWebSocketFrameReceived("req-1", "hello", isBinary: false);

        var streams = listener.GetPendingStreams();
        var frames = streams[0].Frames;
        frames.Should().HaveCount(1);
        frames![0].TextPayload.Should().Be("hello");
        frames[0].Direction.Should().Be(StreamFrameDirection.Received);
    }

    [Fact]
    public void HandleWebSocketFrameReceived_RespectsMaxFrames()
    {
        var listener = CreateListener(captureSamples: true, maxFrames: 2);
        var sessionId = Guid.NewGuid();
        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");
        listener.HandleWebSocketFrameReceived("req-1", "frame1", isBinary: false);
        listener.HandleWebSocketFrameReceived("req-1", "frame2", isBinary: false);
        listener.HandleWebSocketFrameReceived("req-1", "frame3", isBinary: false);

        var streams = listener.GetPendingStreams();
        // Only 2 frames stored, but frameCount should be 3
        streams[0].Frames.Should().NotBeNull().And.HaveCount(2);
        streams[0].Metadata.Properties["frameCount"].Should().Be("3");
    }

    [Fact]
    public void HandleWebSocketFrameReceived_MetadataOnly_WhenCaptureSamplesDisabled()
    {
        var listener = CreateListener(captureSamples: false);
        var sessionId = Guid.NewGuid();
        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");
        listener.HandleWebSocketFrameReceived("req-1", "hello", isBinary: false);
        listener.HandleWebSocketFrameSent("req-1", "world", isBinary: false);

        var streams = listener.GetPendingStreams();
        streams[0].Frames.Should().BeNull();
        streams[0].Metadata.Properties["frameCount"].Should().Be("2");
    }

    [Fact]
    public void HandleWebSocketClosed_SetsEndedAt()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandleWebSocketCreated(sessionId, "req-1", "wss://example.com/ws");
        listener.HandleWebSocketClosed("req-1");

        var streams = listener.GetPendingStreams();
        streams[0].EndedAt.Should().NotBeNull();
    }
}
