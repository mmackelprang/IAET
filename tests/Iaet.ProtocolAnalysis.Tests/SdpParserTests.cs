using FluentAssertions;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class SdpParserTests
{
    private const string SampleSdp = """
        v=0
        o=- 1234567890 2 IN IP4 127.0.0.1
        s=-
        t=0 0
        a=group:BUNDLE 0 1
        m=audio 9 UDP/TLS/RTP/SAVPF 111 103
        c=IN IP4 0.0.0.0
        a=rtpmap:111 opus/48000/2
        a=rtpmap:103 ISAC/16000
        a=ice-ufrag:abc123
        a=ice-pwd:def456
        a=fingerprint:sha-256 AA:BB:CC
        m=video 9 UDP/TLS/RTP/SAVPF 96 97
        a=rtpmap:96 VP8/90000
        a=rtpmap:97 H264/90000
        """;

    [Fact]
    public void Parse_extracts_media_sections()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.MediaSections.Should().HaveCount(2);
        result.MediaSections[0].Type.Should().Be("audio");
        result.MediaSections[1].Type.Should().Be("video");
    }

    [Fact]
    public void Parse_extracts_codecs()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.MediaSections[0].Codecs.Should().Contain(["opus/48000/2", "ISAC/16000"]);
        result.MediaSections[1].Codecs.Should().Contain(["VP8/90000", "H264/90000"]);
    }

    [Fact]
    public void Parse_extracts_ice_credentials()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.IceUfrag.Should().Be("abc123");
        result.IcePwd.Should().Be("def456");
    }

    [Fact]
    public void Parse_extracts_fingerprint()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.Fingerprint.Should().Be("sha-256 AA:BB:CC");
    }

    [Fact]
    public void Parse_extracts_bundle_group()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.BundleGroup.Should().Be("0 1");
    }

    [Fact]
    public void Parse_handles_empty_sdp()
    {
        var result = SdpParser.Parse("");

        result.MediaSections.Should().BeEmpty();
    }
}
