using FluentAssertions;
using Iaet.Capture;
using Iaet.Capture.Listeners;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests.Listeners;

public class MediaStreamListenerTests
{
    private static MediaStreamListener CreateListener()
    {
        var options = new StreamCaptureOptions { Enabled = true };
        return new MediaStreamListener(options);
    }

    [Fact]
    public void IsMediaManifest_DetectsHlsByUrl()
    {
        MediaStreamListener.IsMediaManifest("https://cdn.example.com/stream.m3u8", null).Should().BeTrue();
        MediaStreamListener.IsMediaManifest("https://cdn.example.com/stream.mpd", null).Should().BeTrue();
        MediaStreamListener.IsMediaManifest("https://cdn.example.com/stream.mp4", null).Should().BeFalse();
    }

    [Fact]
    public void IsMediaManifest_DetectsByContentType()
    {
        MediaStreamListener.IsMediaManifest("https://example.com/manifest", "application/vnd.apple.mpegurl").Should().BeTrue();
        MediaStreamListener.IsMediaManifest("https://example.com/manifest", "application/dash+xml").Should().BeTrue();
        MediaStreamListener.IsMediaManifest("https://example.com/page", "text/html").Should().BeFalse();
    }

    [Fact]
    public void HandleManifestDetected_CreatesStream()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandleManifestDetected(sessionId, "https://cdn.example.com/stream.m3u8", "HLS");

        var streams = listener.GetPendingStreams();
        streams.Should().HaveCount(1);
        streams[0].Url.Should().Be("https://cdn.example.com/stream.m3u8");
        streams[0].SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void HandleManifestDetected_SetsCorrectProtocol()
    {
        var listener = CreateListener();
        var sessionId = Guid.NewGuid();
        listener.HandleManifestDetected(sessionId, "https://cdn.example.com/stream.m3u8", "HLS");
        listener.HandleManifestDetected(sessionId, "https://cdn.example.com/stream.mpd", "DASH");

        var streams = listener.GetPendingStreams();
        streams.Should().HaveCount(2);
        streams.Should().ContainSingle(s => s.Protocol == StreamProtocol.HlsStream);
        streams.Should().ContainSingle(s => s.Protocol == StreamProtocol.DashStream);
    }
}
