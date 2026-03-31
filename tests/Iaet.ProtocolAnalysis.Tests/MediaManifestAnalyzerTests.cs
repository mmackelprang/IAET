using FluentAssertions;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class MediaManifestAnalyzerTests
{
    [Fact]
    public void AnalyzeHls_extracts_variants()
    {
        var manifest = """
            #EXTM3U
            #EXT-X-STREAM-INF:BANDWIDTH=1000000,RESOLUTION=640x360,CODECS="avc1.42e00a,mp4a.40.2"
            low/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1280x720,CODECS="avc1.4d401f,mp4a.40.2"
            mid/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=6000000,RESOLUTION=1920x1080,CODECS="avc1.640028,mp4a.40.2"
            high/index.m3u8
            """;

        var result = MediaManifestAnalyzer.AnalyzeHls(manifest);

        result.Variants.Should().HaveCount(3);
        result.Variants[0].Resolution.Should().Be("640x360");
        result.Variants[2].Resolution.Should().Be("1920x1080");
    }

    [Fact]
    public void AnalyzeHls_handles_empty_manifest()
    {
        var result = MediaManifestAnalyzer.AnalyzeHls("");
        result.Variants.Should().BeEmpty();
    }

    [Fact]
    public void DetectFormat_identifies_hls_vs_dash()
    {
        MediaManifestAnalyzer.DetectFormat("#EXTM3U\n").Should().Be("HLS");
        MediaManifestAnalyzer.DetectFormat("<MPD xmlns=").Should().Be("DASH");
        MediaManifestAnalyzer.DetectFormat("unknown").Should().Be("Unknown");
    }
}
