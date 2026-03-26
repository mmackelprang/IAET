using FluentAssertions;
using Iaet.Capture;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests.Listeners;

public class WebRtcListenerTests
{
    private static WebRtcListener CreateListener()
    {
        var options = new StreamCaptureOptions { Enabled = true };
        return new WebRtcListener(options);
    }

    [Fact]
    public void ProtocolName_IsWebRtc()
    {
        var listener = CreateListener();
        listener.ProtocolName.Should().Be("WebRTC");
    }

    [Fact]
    public void HandlePeerConnectionCreated_CreatesStream()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandlePeerConnectionCreated(sessionId, "conn-1");

        var streams = listener.GetPendingStreams();
        streams.Should().HaveCount(1);
        streams[0].Url.Should().Be("webrtc://conn-1");
        streams[0].Protocol.Should().Be(StreamProtocol.WebRtc);
        streams[0].SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void HandleSdpExchange_StoresSdpInMetadata()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandlePeerConnectionCreated(sessionId, "conn-1");
        listener.HandleSdpExchange("conn-1", "offer", "v=0\r\no=- 12345 ...");

        var streams = listener.GetPendingStreams();
        streams[0].Metadata.Properties["sdp.offer"].Should().Be("v=0\r\no=- 12345 ...");
    }

    [Fact]
    public void HandleIceCandidate_IncrementsCount()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandlePeerConnectionCreated(sessionId, "conn-1");
        listener.HandleIceCandidate("conn-1", "candidate:1 1 UDP ...");
        listener.HandleIceCandidate("conn-1", "candidate:2 1 UDP ...");

        var streams = listener.GetPendingStreams();
        streams[0].Metadata.Properties["iceCandidateCount"].Should().Be("2");
    }
}
