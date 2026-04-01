using FluentAssertions;
using Iaet.Android.Bluetooth;

namespace Iaet.Android.Tests.Bluetooth;

public sealed class HciLogImporterTests
{
    [Fact]
    public void Parse_validates_magic_header()
    {
        var bad = new byte[16];
        var result = HciLogImporter.Parse(bad);
        result.Errors.Should().Contain(e => e.Contains("Invalid BTSnoop magic"));
    }

    [Fact]
    public void Parse_validates_minimum_size()
    {
        var result = HciLogImporter.Parse(new byte[10]);
        result.Errors.Should().Contain(e => e.Contains("too short"));
    }

    [Fact]
    public void Parse_rejects_unsupported_version()
    {
        var data = BuildBtSnoopHeader(version: 2);
        var result = HciLogImporter.Parse(data);
        result.Errors.Should().Contain(e => e.Contains("Unsupported BTSnoop version"));
    }

    [Fact]
    public void Parse_accepts_valid_header_with_no_packets()
    {
        var data = BuildBtSnoopHeader();
        var result = HciLogImporter.Parse(data);
        result.Errors.Should().BeEmpty();
        result.TotalPackets.Should().Be(0);
        result.AttPackets.Should().Be(0);
    }

    [Fact]
    public void Parse_extracts_att_notification()
    {
        var data = BuildBtSnoopWithAttPacket(0x1B, 0x0015, [0x64]);
        var result = HciLogImporter.Parse(data);

        result.Errors.Should().BeEmpty();
        result.TotalPackets.Should().Be(1);
        result.AttPackets.Should().Be(1);
        result.Operations.Should().ContainSingle();

        var op = result.Operations[0];
        op.Type.Should().Be("Notify");
        op.Handle.Should().Be(0x0015);
        op.IsReceived.Should().BeTrue();
        op.Value.Should().NotBeNull();
        op.Value!.Value.AsSpan().ToArray().Should().Equal(0x64);
        op.ValueLength.Should().Be(1);
    }

    [Fact]
    public void Parse_extracts_att_write_request()
    {
        var data = BuildBtSnoopWithAttPacket(0x12, 0x0020, [0x01, 0x02], isReceived: false);
        var result = HciLogImporter.Parse(data);

        result.AttPackets.Should().Be(1);
        var op = result.Operations[0];
        op.Type.Should().Be("Write");
        op.Handle.Should().Be(0x0020);
        op.IsReceived.Should().BeFalse();
        op.Value.Should().NotBeNull();
        op.Value!.Value.AsSpan().ToArray().Should().Equal(0x01, 0x02);
    }

    [Fact]
    public void Parse_extracts_att_write_command()
    {
        var data = BuildBtSnoopWithAttPacket(0x52, 0x0010, [0xAB]);
        var result = HciLogImporter.Parse(data);

        result.AttPackets.Should().Be(1);
        result.Operations[0].Type.Should().Be("WriteCommand");
    }

    [Fact]
    public void Parse_extracts_att_read_request()
    {
        // Read Request: opcode 0x0A + 2-byte handle, no value
        var data = BuildBtSnoopWithAttPacket(0x0A, 0x0003, [], isReceived: false);
        var result = HciLogImporter.Parse(data);

        result.AttPackets.Should().Be(1);
        var op = result.Operations[0];
        op.Type.Should().Be("Read");
        op.Handle.Should().Be(0x0003);
        op.ValueLength.Should().Be(0);
    }

    [Fact]
    public void Parse_extracts_att_read_response()
    {
        // Read Response: opcode 0x0B + value bytes (no handle)
        var data = BuildBtSnoopWithReadResponse([0x64, 0x00]);
        var result = HciLogImporter.Parse(data);

        result.AttPackets.Should().Be(1);
        var op = result.Operations[0];
        op.Type.Should().Be("ReadResponse");
        op.Handle.Should().Be(0); // handle not in response
        op.Value.Should().NotBeNull();
        op.Value!.Value.AsSpan().ToArray().Should().Equal(0x64, 0x00);
        op.ValueLength.Should().Be(2);
    }

    [Fact]
    public void Parse_extracts_att_indication()
    {
        var data = BuildBtSnoopWithAttPacket(0x1D, 0x0018, [0xFF]);
        var result = HciLogImporter.Parse(data);

        result.AttPackets.Should().Be(1);
        result.Operations[0].Type.Should().Be("Indicate");
    }

    [Fact]
    public void Parse_ignores_non_att_acl_packets()
    {
        // Build an ACL packet with a non-ATT L2CAP channel (e.g., 0x0001 = signaling)
        var data = BuildBtSnoopWithNonAttPacket();
        var result = HciLogImporter.Parse(data);

        result.TotalPackets.Should().Be(1);
        result.AttPackets.Should().Be(0);
        result.Operations.Should().BeEmpty();
    }

    [Fact]
    public void Parse_populates_L2capChannelCounts_for_att_packets()
    {
        var data = BuildBtSnoopWithAttPacket(0x1B, 0x0015, [0x64]);
        var result = HciLogImporter.Parse(data);

        result.L2capChannelCounts.Should().ContainKey(0x0004);
        result.L2capChannelCounts[0x0004].Should().Be(1);
    }

    [Fact]
    public void Parse_counts_all_channels_across_multiple_packets()
    {
        // Two ATT packets + one signaling packet
        var pkt1 = BuildAttAclRecord(0x1B, 0x0015, [0x64], isReceived: true);
        var pkt2 = BuildAttAclRecord(0x12, 0x0020, [0x01], isReceived: false);
        var pkt3 = BuildNonAttAclRecord(); // channel 0x0001

        var header = BuildBtSnoopHeader();
        var data = Concat(header, pkt1, pkt2, pkt3);
        var result = HciLogImporter.Parse(data);

        result.L2capChannelCounts.Should().ContainKey(0x0004);
        result.L2capChannelCounts[0x0004].Should().Be(2);
        result.L2capChannelCounts.Should().ContainKey(0x0001);
        result.L2capChannelCounts[0x0001].Should().Be(1);
    }

    [Fact]
    public void Parse_extracts_dynamic_channel_payload()
    {
        // Build a packet on dynamic channel 0x0040 with payload [0xAB, 0x02, 0x11, 0x22]
        var payload = new byte[] { 0xAB, 0x02, 0x11, 0x22 };
        var record = BuildAclRecord(payload, channelId: 0x0040, isReceived: true);
        var header = BuildBtSnoopHeader();
        var data = Concat(header, record);

        var result = HciLogImporter.Parse(data);

        result.TotalPackets.Should().Be(1);
        result.AttPackets.Should().Be(0);
        result.L2capData.Should().ContainSingle();

        var l2cap = result.L2capData[0];
        l2cap.ChannelId.Should().Be(0x0040);
        l2cap.IsReceived.Should().BeTrue();
        l2cap.PayloadLength.Should().Be(4);
        l2cap.Payload.AsSpan().ToArray().Should().Equal(0xAB, 0x02, 0x11, 0x22);
    }

    [Fact]
    public void Parse_does_not_emit_L2capData_for_att_channel()
    {
        var data = BuildBtSnoopWithAttPacket(0x1B, 0x0015, [0x64]);
        var result = HciLogImporter.Parse(data);

        // ATT channel 0x0004 is below DynamicChannelMin (0x0040)
        result.L2capData.Should().BeEmpty();
    }

    [Fact]
    public void Parse_collects_multiple_dynamic_channel_packets()
    {
        var payload1 = new byte[] { 0x01, 0x02 };
        var payload2 = new byte[] { 0x03, 0x04, 0x05 };
        var pkt1 = BuildAclRecord(payload1, channelId: 0x0040, isReceived: true);
        var pkt2 = BuildAclRecord(payload2, channelId: 0x0400, isReceived: false);

        var header = BuildBtSnoopHeader();
        var data = Concat(header, pkt1, pkt2);
        var result = HciLogImporter.Parse(data);

        result.L2capData.Should().HaveCount(2);
        result.L2capData[0].ChannelId.Should().Be(0x0040);
        result.L2capData[1].ChannelId.Should().Be(0x0400);
        result.L2capChannelCounts.Should().ContainKey(0x0040);
        result.L2capChannelCounts.Should().ContainKey(0x0400);
    }

    [Fact]
    public void Parse_handles_multiple_packets()
    {
        var pkt1 = BuildAttAclRecord(0x1B, 0x0015, [0x64], isReceived: true);
        var pkt2 = BuildAttAclRecord(0x12, 0x0020, [0x01], isReceived: false);

        var header = BuildBtSnoopHeader();
        var data = new byte[header.Length + pkt1.Length + pkt2.Length];
        Array.Copy(header, data, header.Length);
        Array.Copy(pkt1, 0, data, header.Length, pkt1.Length);
        Array.Copy(pkt2, 0, data, header.Length + pkt1.Length, pkt2.Length);

        var result = HciLogImporter.Parse(data);

        result.TotalPackets.Should().Be(2);
        result.AttPackets.Should().Be(2);
        result.Operations.Should().HaveCount(2);
        result.Operations[0].Type.Should().Be("Notify");
        result.Operations[1].Type.Should().Be("Write");
    }

    [Fact]
    public void Parse_stops_gracefully_on_truncated_packet()
    {
        var data = BuildBtSnoopWithAttPacket(0x1B, 0x0015, [0x64]);
        // Truncate the last byte to simulate corruption
        var truncated = data[..^1];
        var result = HciLogImporter.Parse(truncated);

        // Should not crash; may or may not parse the truncated packet
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_throws_on_null_input()
    {
        var act = () => HciLogImporter.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseFile_returns_error_for_missing_file()
    {
        var result = HciLogImporter.ParseFile("/nonexistent/path.log");
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public void ParseFile_throws_on_null_path()
    {
        var act = () => HciLogImporter.ParseFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Parse_sets_timestamp_from_btsnoop_epoch()
    {
        var data = BuildBtSnoopWithAttPacket(0x1B, 0x0015, [0x64]);
        var result = HciLogImporter.Parse(data);

        // Timestamp should be based on BtSnoop epoch (2000-01-01) plus the encoded microseconds
        result.Operations[0].Timestamp.Year.Should().BeGreaterThanOrEqualTo(2000);
    }

    #region Test Helpers

    private static byte[] BuildBtSnoopHeader(uint version = 1)
    {
        var data = new byte[HeaderSize];
        "btsnoop\0"u8.CopyTo(data);
        // Version (big-endian)
        data[8] = (byte)(version >> 24);
        data[9] = (byte)(version >> 16);
        data[10] = (byte)(version >> 8);
        data[11] = (byte)(version & 0xFF);
        // Datalink type 1002 = HCI UART (big-endian)
        data[12] = 0;
        data[13] = 0;
        data[14] = 0x03;
        data[15] = 0xEA;
        return data;
    }

    private const int HeaderSize = 16;
    private const int RecordHeaderSize = 24;

    private static byte[] BuildAttAclRecord(byte attOpcode, ushort handle, byte[] value, bool isReceived)
    {
        // ATT payload: opcode + handle(LE) + value
        var attPayload = new byte[3 + value.Length];
        attPayload[0] = attOpcode;
        attPayload[1] = (byte)(handle & 0xFF);
        attPayload[2] = (byte)(handle >> 8);
        Array.Copy(value, 0, attPayload, 3, value.Length);

        return BuildAclRecord(attPayload, 0x0004, isReceived);
    }

    private static byte[] BuildAclRecord(byte[] l2capPayload, ushort channelId, bool isReceived)
    {
        // L2CAP header: length(LE) + channel(LE)
        var l2capLen = l2capPayload.Length;
        var l2cap = new byte[4 + l2capLen];
        l2cap[0] = (byte)(l2capLen & 0xFF);
        l2cap[1] = (byte)(l2capLen >> 8);
        l2cap[2] = (byte)(channelId & 0xFF);
        l2cap[3] = (byte)(channelId >> 8);
        Array.Copy(l2capPayload, 0, l2cap, 4, l2capLen);

        // ACL header: handle+flags(2) + length(2)
        var aclLen = l2cap.Length;
        var acl = new byte[4 + aclLen];
        acl[0] = 0x01;
        acl[1] = 0x20; // handle 1, PB=first auto-flush
        acl[2] = (byte)(aclLen & 0xFF);
        acl[3] = (byte)(aclLen >> 8);
        Array.Copy(l2cap, 0, acl, 4, aclLen);

        // BTSnoop packet record header
        var packetLen = acl.Length;
        var record = new byte[RecordHeaderSize + packetLen];

        // Original length (BE)
        record[0] = 0;
        record[1] = 0;
        record[2] = (byte)(packetLen >> 8);
        record[3] = (byte)(packetLen & 0xFF);
        // Included length (BE)
        record[4] = 0;
        record[5] = 0;
        record[6] = (byte)(packetLen >> 8);
        record[7] = (byte)(packetLen & 0xFF);
        // Flags: bit 0 = received
        record[11] = (byte)(isReceived ? 0x01 : 0x00);
        // Drops: 0 (bytes 12-15 already zero)
        // Timestamp: a reasonable value (bytes 16-23)
        // Use 1_000_000_000 microseconds from epoch (~2031 in BtSnoop epoch)
        var ts = 1_000_000_000L;
        record[16] = (byte)(ts >> 56);
        record[17] = (byte)(ts >> 48);
        record[18] = (byte)(ts >> 40);
        record[19] = (byte)(ts >> 32);
        record[20] = (byte)(ts >> 24);
        record[21] = (byte)(ts >> 16);
        record[22] = (byte)(ts >> 8);
        record[23] = (byte)(ts & 0xFF);

        Array.Copy(acl, 0, record, RecordHeaderSize, packetLen);
        return record;
    }

    private static byte[] BuildBtSnoopWithAttPacket(byte attOpcode, ushort handle, byte[] value, bool isReceived = true)
    {
        var record = BuildAttAclRecord(attOpcode, handle, value, isReceived);
        var header = BuildBtSnoopHeader();
        var file = new byte[header.Length + record.Length];
        Array.Copy(header, file, header.Length);
        Array.Copy(record, 0, file, header.Length, record.Length);
        return file;
    }

    private static byte[] BuildBtSnoopWithReadResponse(byte[] value)
    {
        // Read Response: opcode 0x0B + value (no handle field)
        var attPayload = new byte[1 + value.Length];
        attPayload[0] = 0x0B;
        Array.Copy(value, 0, attPayload, 1, value.Length);

        var record = BuildAclRecord(attPayload, 0x0004, isReceived: true);
        var header = BuildBtSnoopHeader();
        var file = new byte[header.Length + record.Length];
        Array.Copy(header, file, header.Length);
        Array.Copy(record, 0, file, header.Length, record.Length);
        return file;
    }

    private static byte[] BuildBtSnoopWithNonAttPacket()
    {
        // L2CAP signaling channel (0x0001) with some data
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var record = BuildAclRecord(payload, 0x0001, isReceived: true);
        var header = BuildBtSnoopHeader();
        var file = new byte[header.Length + record.Length];
        Array.Copy(header, file, header.Length);
        Array.Copy(record, 0, file, header.Length, record.Length);
        return file;
    }

    /// <summary>Single BTSnoop record on L2CAP signaling channel 0x0001 (non-ATT).</summary>
    private static byte[] BuildNonAttAclRecord()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        return BuildAclRecord(payload, 0x0001, isReceived: true);
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var totalLen = 0;
        foreach (var part in parts)
            totalLen += part.Length;

        var result = new byte[totalLen];
        var pos = 0;
        foreach (var part in parts)
        {
            Array.Copy(part, 0, result, pos, part.Length);
            pos += part.Length;
        }

        return result;
    }

    #endregion
}
