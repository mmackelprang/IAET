using FluentAssertions;
using Iaet.Capture;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests.Listeners;

public class GrpcWebListenerTests
{
    private static GrpcWebListener CreateListener()
    {
        var options = new StreamCaptureOptions { Enabled = true };
        return new GrpcWebListener(options);
    }

    [Fact]
    public void IsGrpcOrProtobuf_DetectsContentTypes()
    {
        GrpcWebListener.IsGrpcOrProtobuf("application/grpc-web").Should().BeTrue();
        GrpcWebListener.IsGrpcOrProtobuf("application/grpc-web+proto").Should().BeTrue();
        GrpcWebListener.IsGrpcOrProtobuf("application/x-protobuf").Should().BeTrue();
        GrpcWebListener.IsGrpcOrProtobuf("application/json").Should().BeFalse();
        GrpcWebListener.IsGrpcOrProtobuf("text/html").Should().BeFalse();
    }

    [Fact]
    public void HandleGrpcDetected_CreatesStream()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandleGrpcDetected(sessionId, "https://api.example.com/proto.Service/Method", "application/grpc-web+proto", 512);

        var streams = listener.GetPendingStreams();
        streams.Should().HaveCount(1);
        streams[0].Url.Should().Be("https://api.example.com/proto.Service/Method");
        streams[0].Protocol.Should().Be(StreamProtocol.GrpcWeb);
        streams[0].SessionId.Should().Be(sessionId);
        streams[0].Metadata.Properties["contentType"].Should().Be("application/grpc-web+proto");
        streams[0].Metadata.Properties["bodySize"].Should().Be("512");
    }
}
