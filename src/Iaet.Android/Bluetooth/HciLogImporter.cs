using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Iaet.Android.Bluetooth;

/// <summary>Result of parsing a BTSnoop HCI log file.</summary>
public sealed record HciLogResult
{
    public int TotalPackets { get; init; }
    public int AttPackets { get; init; }
    public IReadOnlyList<AttOperation> Operations { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>All L2CAP dynamic channel (channel ID &gt;= 0x0040) packets observed.</summary>
    public IReadOnlyList<L2capChannelData> L2capData { get; init; } = [];

    /// <summary>Packet count keyed by L2CAP channel ID (all channels, including ATT 0x0004).</summary>
    public IReadOnlyDictionary<ushort, int> L2capChannelCounts { get; init; } = new Dictionary<ushort, int>();
}

/// <summary>Payload extracted from a single L2CAP dynamic channel packet.</summary>
public sealed record L2capChannelData
{
    public required ushort ChannelId { get; init; }
    public required bool IsReceived { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required ImmutableArray<byte> Payload { get; init; }
    public int PayloadLength { get; init; }
}

/// <summary>A single ATT protocol operation observed in HCI traffic.</summary>
public sealed record AttOperation
{
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// ATT operation type: "Read", "ReadResponse", "Write", "WriteCommand", "Notify", or "Indicate".
    /// </summary>
    public required string Type { get; init; }

    public required ushort Handle { get; init; }
    public required bool IsReceived { get; init; }
    public ImmutableArray<byte>? Value { get; init; }
    public int ValueLength { get; init; }
}

/// <summary>
/// Parses Android's btsnoop_hci.log binary format to extract BLE ATT operations.
/// The BTSnoop format records HCI packets; this importer finds ATT protocol operations
/// (reads, writes, notifications, indications) carried over L2CAP channel 0x0004.
/// </summary>
public static class HciLogImporter
{
    private static readonly byte[] BtSnoopMagic = "btsnoop\0"u8.ToArray();
    private static readonly DateTimeOffset BtSnoopEpoch = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const int HeaderSize = 16;
    private const int PacketRecordHeaderSize = 24;
    private const ushort AttChannelId = 0x0004;

    /// <summary>L2CAP dynamic channels start at 0x0040 per the BLE spec.</summary>
    private const ushort DynamicChannelMin = 0x0040;

    /// <summary>Parse a BTSnoop HCI log from a byte array.</summary>
    public static HciLogResult Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < HeaderSize)
            return new HciLogResult { Errors = ["File too short for BTSnoop header"] };

        // Verify magic
        if (!data.AsSpan(0, 8).SequenceEqual(BtSnoopMagic))
            return new HciLogResult { Errors = ["Invalid BTSnoop magic header"] };

        var version = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8, 4));
        if (version != 1)
            return new HciLogResult { Errors = [$"Unsupported BTSnoop version: {version}"] };

        var operations = new List<AttOperation>();
        var l2capData = new List<L2capChannelData>();
        var channelCounts = new Dictionary<ushort, int>();
        var totalPackets = 0;
        var attPackets = 0;
        var errors = new List<string>();
        var offset = HeaderSize;

        while (offset + PacketRecordHeaderSize <= data.Length)
        {
            var includedLen = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 4, 4));
            var flags = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8, 4));
            var timestampUs = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(offset + 16, 8));

            var isReceived = (flags & 1) != 0;
            var packetDataOffset = offset + PacketRecordHeaderSize;

            if (packetDataOffset + includedLen > data.Length)
                break;

            totalPackets++;
            var timestamp = BtSnoopEpoch.AddTicks(timestampUs * 10); // microseconds to ticks

            var packetSpan = data.AsSpan(packetDataOffset, includedLen);

            // Track L2CAP channel and extract payload for all ACL packets
            TryProcessL2cap(packetSpan, timestamp, isReceived, channelCounts, l2capData);

            // Try to parse as ACL data with ATT payload (channel 0x0004)
            var attOp = TryParseAttFromAcl(packetSpan, timestamp, isReceived);

            if (attOp is not null)
            {
                attPackets++;
                operations.Add(attOp);
            }

            offset = packetDataOffset + includedLen;
        }

        return new HciLogResult
        {
            TotalPackets = totalPackets,
            AttPackets = attPackets,
            Operations = operations,
            Errors = errors,
            L2capData = l2capData,
            L2capChannelCounts = channelCounts,
        };
    }

    /// <summary>Parse a BTSnoop HCI log file from disk.</summary>
    public static HciLogResult ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
            return new HciLogResult { Errors = [$"File not found: {path}"] };

        return Parse(File.ReadAllBytes(path));
    }

    /// <summary>
    /// Inspect an HCI ACL packet for L2CAP header information: track channel counts and, for
    /// dynamic channels (&gt;= 0x0040), capture the payload into <paramref name="l2capData"/>.
    /// </summary>
    private static void TryProcessL2cap(
        ReadOnlySpan<byte> packet,
        DateTimeOffset timestamp,
        bool isReceived,
        Dictionary<ushort, int> channelCounts,
        List<L2capChannelData> l2capData)
    {
        // Minimum: 4 ACL header + 4 L2CAP header = 8 bytes
        if (packet.Length < 8)
            return;

        const int l2capOffset = 4;
        var channelId = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(l2capOffset + 2, 2));

        channelCounts[channelId] = channelCounts.TryGetValue(channelId, out var count) ? count + 1 : 1;

        if (channelId < DynamicChannelMin)
            return;

        var l2capLength = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(l2capOffset, 2));
        var payloadStart = l2capOffset + 4;
        var availablePayload = packet.Length - payloadStart;

        if (availablePayload <= 0)
            return;

        // Clamp to what is actually present (handles truncated packets)
        var payloadLength = Math.Min(l2capLength, availablePayload);
        var payload = ImmutableArray.Create(packet.Slice(payloadStart, payloadLength));

        l2capData.Add(new L2capChannelData
        {
            ChannelId = channelId,
            IsReceived = isReceived,
            Timestamp = timestamp,
            Payload = payload,
            PayloadLength = payloadLength,
        });
    }

    /// <summary>
    /// Attempt to extract an ATT operation from an HCI ACL data packet.
    /// HCI ACL: 4-byte header, then L2CAP (2-byte length + 2-byte channel ID), then ATT payload.
    /// </summary>
    private static AttOperation? TryParseAttFromAcl(ReadOnlySpan<byte> packet, DateTimeOffset timestamp, bool isReceived)
    {
        // Minimum: 4 ACL header + 4 L2CAP header + 1 ATT opcode = 9 bytes
        if (packet.Length < 9)
            return null;

        // ACL header: 2 bytes handle+flags, 2 bytes data total length
        // L2CAP starts at offset 4
        const int l2capOffset = 4;

        var channelId = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(l2capOffset + 2, 2));

        if (channelId != AttChannelId)
            return null;

        var l2capLength = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(l2capOffset, 2));
        var attStart = l2capOffset + 4;
        var attLength = Math.Min(l2capLength, packet.Length - attStart);

        if (attLength < 1)
            return null;

        return ParseAttPacket(packet.Slice(attStart, attLength), timestamp, isReceived);
    }

    private static AttOperation? ParseAttPacket(ReadOnlySpan<byte> att, DateTimeOffset timestamp, bool isReceived)
    {
        if (att.Length < 1)
            return null;

        var opcode = att[0];
        return opcode switch
        {
            0x0A when att.Length >= 3 => new AttOperation  // Read Request
            {
                Timestamp = timestamp,
                Type = "Read",
                IsReceived = isReceived,
                Handle = BinaryPrimitives.ReadUInt16LittleEndian(att.Slice(1, 2)),
                ValueLength = 0,
            },
            0x0B when att.Length >= 2 => new AttOperation  // Read Response
            {
                Timestamp = timestamp,
                Type = "ReadResponse",
                IsReceived = isReceived,
                Handle = 0, // handle not present in read response
                Value = [.. att[1..]],
                ValueLength = att.Length - 1,
            },
            0x12 when att.Length >= 3 => new AttOperation  // Write Request
            {
                Timestamp = timestamp,
                Type = "Write",
                IsReceived = isReceived,
                Handle = BinaryPrimitives.ReadUInt16LittleEndian(att.Slice(1, 2)),
                Value = att.Length > 3 ? [.. att[3..]] : null,
                ValueLength = Math.Max(0, att.Length - 3),
            },
            0x52 when att.Length >= 3 => new AttOperation  // Write Command (no response)
            {
                Timestamp = timestamp,
                Type = "WriteCommand",
                IsReceived = isReceived,
                Handle = BinaryPrimitives.ReadUInt16LittleEndian(att.Slice(1, 2)),
                Value = att.Length > 3 ? [.. att[3..]] : null,
                ValueLength = Math.Max(0, att.Length - 3),
            },
            0x1B when att.Length >= 3 => new AttOperation  // Handle Value Notification
            {
                Timestamp = timestamp,
                Type = "Notify",
                IsReceived = isReceived,
                Handle = BinaryPrimitives.ReadUInt16LittleEndian(att.Slice(1, 2)),
                Value = att.Length > 3 ? [.. att[3..]] : null,
                ValueLength = Math.Max(0, att.Length - 3),
            },
            0x1D when att.Length >= 3 => new AttOperation  // Handle Value Indication
            {
                Timestamp = timestamp,
                Type = "Indicate",
                IsReceived = isReceived,
                Handle = BinaryPrimitives.ReadUInt16LittleEndian(att.Slice(1, 2)),
                Value = att.Length > 3 ? [.. att[3..]] : null,
                ValueLength = Math.Max(0, att.Length - 3),
            },
            _ => null,
        };
    }
}
