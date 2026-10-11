using System.Text;

namespace Iaet.Native.Extractors;

public sealed record ExtractedString(string Value, long Offset, string Encoding);

/// <summary>
/// Pulls printable string runs out of arbitrary bytes, the way <c>strings(1)</c> does, plus
/// UTF-16LE runs that Windows and some SDKs embed. Strings are the cheapest evidence a binary
/// yields and the first thing an analyst searches.
/// </summary>
public static class BinaryStringExtractor
{
    public static IReadOnlyList<ExtractedString> Extract(ReadOnlySpan<byte> bytes, int minLength = 5, int maxStrings = 50_000)
    {
        var results = new List<ExtractedString>();
        if (minLength < 1)
            minLength = 1;

        // ASCII runs
        var start = -1;
        for (var i = 0; i <= bytes.Length; i++)
        {
            var printable = i < bytes.Length && IsPrintableAscii(bytes[i]);
            if (printable)
            {
                if (start < 0)
                    start = i;
                continue;
            }
            if (start >= 0)
            {
                var len = i - start;
                if (len >= minLength)
                {
                    results.Add(new ExtractedString(Encoding.ASCII.GetString(bytes.Slice(start, len)), start, "ascii"));
                    if (results.Count >= maxStrings)
                        return results;
                }
                start = -1;
            }
        }

        // UTF-16LE runs: printable ASCII byte followed by 0x00, repeated.
        start = -1;
        var count = 0;
        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            var isChar = bytes[i + 1] == 0 && IsPrintableAscii(bytes[i]);
            if (isChar)
            {
                if (start < 0)
                    start = i;
                count++;
                continue;
            }
            if (start >= 0 && count >= minLength)
            {
                results.Add(new ExtractedString(Encoding.Unicode.GetString(bytes.Slice(start, count * 2)), start, "utf-16le"));
                if (results.Count >= maxStrings)
                    return results;
            }
            start = -1;
            count = 0;
        }
        if (start >= 0 && count >= minLength)
            results.Add(new ExtractedString(Encoding.Unicode.GetString(bytes.Slice(start, count * 2)), start, "utf-16le"));

        return results;
    }

    private static bool IsPrintableAscii(byte b) => b is >= 0x20 and <= 0x7E || b == (byte)'\t';
}
