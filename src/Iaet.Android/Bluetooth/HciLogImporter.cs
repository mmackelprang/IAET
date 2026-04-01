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

            // Try to parse as ACL data with ATT payload
            var attOp = TryParseAttFromAcl(
                data.AsSpan(packetDataOffset, includedLen),
                timestamp, isReceived);

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
