using FluentAssertions;
using Iaet.Capture;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests.Listeners;

public class SseListenerTests
{
    private static SseListener CreateListener(bool captureSamples = true)
    {
        var options = new StreamCaptureOptions
        {
            Enabled = true,
            CaptureSamples = captureSamples,
        };
        return new SseListener(options);
    }

    [Fact]
    public void ProtocolName_IsServerSentEvents()
    {
        var listener = CreateListener();
        listener.ProtocolName.Should().Be("ServerSentEvents");
    }

    [Fact]
    public void IsServerSentEvents_ReturnsTrueForTextEventStream()
    {
        SseListener.IsServerSentEvents("text/event-stream").Should().BeTrue();
        SseListener.IsServerSentEvents("text/event-stream; charset=utf-8").Should().BeTrue();
        SseListener.IsServerSentEvents("application/json").Should().BeFalse();
        SseListener.IsServerSentEvents("").Should().BeFalse();
    }

    [Fact]
    public void HandleSseDetected_CreatesStream()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandleSseDetected(sessionId, "https://example.com/events", "text/event-stream");

        var streams = listener.GetPendingStreams();
        streams.Should().HaveCount(1);
        streams[0].Url.Should().Be("https://example.com/events");
        streams[0].Protocol.Should().Be(StreamProtocol.ServerSentEvents);
        streams[0].SessionId.Should().Be(sessionId);
    }
}
