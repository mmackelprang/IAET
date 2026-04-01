using System.Collections.Immutable;
using FluentAssertions;
using Iaet.Android.Bluetooth;

namespace Iaet.Android.Tests.Bluetooth;

public sealed class L2capProtocolExtractorTests
{
    private static readonly DateTimeOffset TestTimestamp = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const ushort DynChannel = 0x0040;

    // -----------------------------------------------------------------------
    // Null-guard
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractFrames_throws_on_null_input()
    {
        var act = () => L2capProtocolExtractor.ExtractFrames(null!, 0xAB);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // Empty / no-match cases
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractFrames_returns_empty_for_empty_list()
    {
        var result = L2capProtocolExtractor.ExtractFrames([], 0xAB);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFrames_returns_empty_when_header_not_present()
    {
        var packet = MakePacket([0x01, 0x02, 0x03, 0x04], channel: DynChannel);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFrames_returns_empty_for_truncated_frame_no_length_byte()
    {
        // Only the header byte, no length byte following it
        var packet = MakePacket([0xAB], channel: DynChannel);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFrames_returns_empty_for_incomplete_frame_data()
    {
        // Header + length=3 but only 2 data bytes present
        var packet = MakePacket([0xAB, 0x03, 0x11, 0x22], channel: DynChannel);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);
        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Single-frame extraction
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractFrames_extracts_single_frame_with_0xAB_header()
    {
        // Frame: [0xAB][0x02][0x11][0x22]
        var packet = MakePacket([0xAB, 0x02, 0x11, 0x22], channel: DynChannel, isReceived: true);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);

        result.Should().ContainSingle();
        var frame = result[0];
        frame.Header.Should().Be(0xAB);
        frame.Payload.AsSpan().ToArray().Should().Equal(0x11, 0x22);
        frame.IsReceived.Should().BeTrue();
        frame.SourceChannel.Should().Be(DynChannel);
        frame.Timestamp.Should().Be(TestTimestamp);
    }

    [Fact]
    public void ExtractFrames_extracts_frame_with_zero_length_payload()
    {
        // Frame: [0xAB][0x00]  — empty payload is valid
        var packet = MakePacket([0xAB, 0x00], channel: DynChannel);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);

        result.Should().ContainSingle();
        result[0].Payload.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFrames_captures_sent_direction()
    {
        var packet = MakePacket([0xAB, 0x01, 0xFF], channel: DynChannel, isReceived: false);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);

        result.Should().ContainSingle();
        result[0].IsReceived.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Multiple frames in one packet
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractFrames_extracts_multiple_frames_from_single_packet()
    {
        // Two back-to-back frames: [AB][01][AA] [AB][02][BB][CC]
        var payload = new byte[] { 0xAB, 0x01, 0xAA, 0xAB, 0x02, 0xBB, 0xCC };
        var packet = MakePacket(payload, channel: DynChannel);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);

        result.Should().HaveCount(2);
        result[0].Payload.AsSpan().ToArray().Should().Equal(0xAA);
        result[1].Payload.AsSpan().ToArray().Should().Equal(0xBB, 0xCC);
    }

    [Fact]
    public void ExtractFrames_skips_non_header_bytes_between_frames()
    {
        // Junk byte between two frames: [01] [AB][01][AA] [FF] [AB][01][BB]
        var payload = new byte[] { 0x01, 0xAB, 0x01, 0xAA, 0xFF, 0xAB, 0x01, 0xBB };
        var packet = MakePacket(payload, channel: DynChannel);
        var result = L2capProtocolExtractor.ExtractFrames([packet], 0xAB);

        result.Should().HaveCount(2);
        result[0].Payload.AsSpan().ToArray().Should().Equal(0xAA);
        result[1].Payload.AsSpan().ToArray().Should().Equal(0xBB);
    }

    // -----------------------------------------------------------------------
    // Multiple packets
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractFrames_extracts_frames_across_multiple_packets()
    {
        var pkt1 = MakePacket([0xAB, 0x01, 0x11], channel: DynChannel, isReceived: true);
        var pkt2 = MakePacket([0xAB, 0x01, 0x22], channel: DynChannel, isReceived: false);
        var result = L2capProtocolExtractor.ExtractFrames([pkt1, pkt2], 0xAB);

        result.Should().HaveCount(2);
        result[0].Payload.AsSpan().ToArray().Should().Equal(0x11);
        result[0].IsReceived.Should().BeTrue();
        result[1].Payload.AsSpan().ToArray().Should().Equal(0x22);
        result[1].IsReceived.Should().BeFalse();
    }

    [Fact]
    public void ExtractFrames_preserves_source_channel_per_frame()
    {
        const ushort chan1 = 0x0040;
        const ushort chan2 = 0x0041;
        var pkt1 = MakePacket([0xAB, 0x01, 0xAA], channel: chan1);
        var pkt2 = MakePacket([0xAB, 0x01, 0xBB], channel: chan2);
        var result = L2capProtocolExtractor.ExtractFrames([pkt1, pkt2], 0xAB);

        result[0].SourceChannel.Should().Be(chan1);
        result[1].SourceChannel.Should().Be(chan2);
    }

    // -----------------------------------------------------------------------
    // Different header byte
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractFrames_works_with_arbitrary_header_byte()
    {
        var packet = MakePacket([0xCD, 0x02, 0x01, 0x02], channel: DynChannel);
        var result = L2capProtocolExtractor.ExtractFrames([packet], headerByte: 0xCD);

        result.Should().ContainSingle();
        result[0].Header.Should().Be(0xCD);
        result[0].Payload.AsSpan().ToArray().Should().Equal(0x01, 0x02);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static L2capChannelData MakePacket(byte[] payload, ushort channel, bool isReceived = true)
    {
        return new L2capChannelData
        {
            ChannelId = channel,
            IsReceived = isReceived,
            Timestamp = TestTimestamp,
            Payload = ImmutableArray.Create(payload),
            PayloadLength = payload.Length,
        };
    }
}
