using System.Collections.Immutable;

namespace Iaet.Android.Bluetooth;

/// <summary>A protocol frame identified by its header byte within an L2CAP payload stream.</summary>
public sealed record L2capProtocolFrame
{
    public required byte Header { get; init; }
    public required ImmutableArray<byte> Payload { get; init; }
    public required bool IsReceived { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required ushort SourceChannel { get; init; }
}

/// <summary>
/// Extracts protocol frames from L2CAP dynamic channel payloads.
/// Useful for devices that bypass GATT/ATT and communicate over L2CAP CoC or LE Credit-Based channels.
/// </summary>
public static class L2capProtocolExtractor
{
    /// <summary>
    /// Extract protocol frames with a specific header byte from L2CAP dynamic channel data.
    /// </summary>
    /// <remarks>
    /// Frame format assumed: <c>[header:1][length:1][data:length]</c>.
    /// For example, <c>headerByte=0xAB</c> extracts Raddy Radio-C protocol frames.
    /// Each L2CAP packet's payload is scanned independently for complete frames matching
    /// the header byte followed by a one-byte length field.
    /// </remarks>
    /// <param name="l2capData">L2CAP packets to scan (typically filtered to dynamic channels).</param>
    /// <param name="headerByte">The first byte of each frame to match.</param>
    /// <returns>All complete frames found, in the order they appear in the input.</returns>
    public static IReadOnlyList<L2capProtocolFrame> ExtractFrames(
        IReadOnlyList<L2capChannelData> l2capData,
        byte headerByte)
    {
        ArgumentNullException.ThrowIfNull(l2capData);

        var frames = new List<L2capProtocolFrame>();

        foreach (var packet in l2capData)
        {
            var payload = packet.Payload;
            var i = 0;

            while (i < payload.Length)
            {
                // Find the next occurrence of headerByte
                if (payload[i] != headerByte)
                {
                    i++;
                    continue;
                }

                // Need at least header byte + length byte
                if (i + 1 >= payload.Length)
                    break;

                var frameLength = payload[i + 1];
                var frameDataStart = i + 2;
                var frameDataEnd = frameDataStart + frameLength;

                // Ensure complete frame is present in this packet
                if (frameDataEnd > payload.Length)
                    break;

                var frameData = ImmutableArray.Create(payload, frameDataStart, frameLength);

                frames.Add(new L2capProtocolFrame
                {
                    Header = headerByte,
                    Payload = frameData,
                    IsReceived = packet.IsReceived,
                    Timestamp = packet.Timestamp,
                    SourceChannel = packet.ChannelId,
                });

                i = frameDataEnd;
            }
        }

        return frames;
    }
}
