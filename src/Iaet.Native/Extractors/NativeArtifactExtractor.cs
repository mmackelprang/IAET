using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.Native.Extractors;

/// <summary>
/// Turns raw strings from a native binary into investigation artifacts: URLs that reveal cloud
/// backends, BLE UUIDs that reveal device protocols, and JNI bindings that connect native code
/// back to the decompiled Java it serves.
/// </summary>
public static partial class NativeArtifactExtractor
{
    [GeneratedRegex(@"\b(?:https?|wss?)://[A-Za-z0-9][A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;=%]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.CultureInvariant)]
    private static partial Regex UuidPattern();

    [GeneratedRegex(@"[%*{}<>|\\""`]|\.\.\.|\$\{|%s|%d", RegexOptions.CultureInvariant)]
    private static partial Regex NoisePattern();

    private static readonly string[] TemplateHosts = ["example.com", "localhost", "www.w3.org", "schemas.android.com", "apache.org", "gnu.org", "openssl.org", "www.ietf.org"];

    public static IReadOnlyList<ExtractedUrl> ExtractUrls(IEnumerable<ExtractedString> strings, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(strings);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ExtractedUrl>();

        foreach (var s in strings)
        {
            foreach (Match m in UrlPattern().Matches(s.Value))
            {
                var value = m.Value.TrimEnd('.', ',', ')', ';', '"', '\'');
                if (!seen.Add(value))
                    continue;

                var host = TryGetHost(value);
                var isTemplate = host is null || TemplateHosts.Any(t => host.EndsWith(t, StringComparison.OrdinalIgnoreCase));
                var hasFormat = NoisePattern().IsMatch(value);
                var confidence = isTemplate ? ConfidenceLevel.Low : hasFormat ? ConfidenceLevel.Medium : ConfidenceLevel.High;

                results.Add(new ExtractedUrl
                {
                    Url = value,
                    SourceFile = sourceFile,
                    Confidence = confidence,
                    Context = hasFormat ? "format string — runtime substitution required" : $"string at offset 0x{s.Offset:X}",
                });
            }
        }
        return results;
    }

    public static IReadOnlyList<string> ExtractBleUuids(IEnumerable<ExtractedString> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in strings)
        {
            foreach (Match m in UuidPattern().Matches(s.Value))
            {
                var uuid = m.Value.ToUpperInvariant();
                // Only all-zero or Bluetooth-base UUIDs are strong BLE signals; others are kept but
                // callers can filter by the base suffix.
                set.Add(uuid);
            }
        }
        return set.ToList();
    }

    public static bool IsBluetoothBaseUuid(string uuid) =>
        uuid is not null && uuid.EndsWith("-0000-1000-8000-00805F9B34FB", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<JniBinding> ExtractJniBindings(IEnumerable<NativeSymbol> exports)
    {
        ArgumentNullException.ThrowIfNull(exports);
        var results = new List<JniBinding>();
        foreach (var sym in exports)
        {
            var binding = JniNameDecoder.Decode(sym.Name);
            if (binding is not null)
                results.Add(binding);
        }
        return results;
    }

    private static string? TryGetHost(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            return uri.Host;
        return null;
    }
}
