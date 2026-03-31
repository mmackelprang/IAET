using System.Text.RegularExpressions;

namespace Iaet.ProtocolAnalysis;

public static partial class MediaManifestAnalyzer
{
    public static HlsManifestResult AnalyzeHls(string manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest))
            return new HlsManifestResult();

        var variants = new List<HlsVariant>();
        var lines = manifest.Split('\n', StringSplitOptions.TrimEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var match = StreamInfPattern().Match(lines[i]);
            if (!match.Success)
                continue;

            var bandwidth = match.Groups[1].Value;
            var resMatch = ResolutionPattern().Match(lines[i]);
            var codecsMatch = CodecsPattern().Match(lines[i]);
            var uri = i + 1 < lines.Length ? lines[i + 1] : null;

            variants.Add(new HlsVariant
            {
                Bandwidth = int.TryParse(bandwidth, out var bw) ? bw : 0,
                Resolution = resMatch.Success ? resMatch.Groups[1].Value : null,
                Codecs = codecsMatch.Success ? codecsMatch.Groups[1].Value : null,
                Uri = uri,
            });
        }

        return new HlsManifestResult { Variants = variants };
    }

    public static string DetectFormat(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Unknown";

        if (content.Contains("#EXTM3U", StringComparison.Ordinal))
            return "HLS";

        if (content.Contains("<MPD", StringComparison.Ordinal))
            return "DASH";

        return "Unknown";
    }

    [GeneratedRegex(@"#EXT-X-STREAM-INF:.*?BANDWIDTH=(\d+)")]
    private static partial Regex StreamInfPattern();

    [GeneratedRegex(@"RESOLUTION=(\d+x\d+)")]
    private static partial Regex ResolutionPattern();

    [GeneratedRegex(@"CODECS=""([^""]+)""")]
    private static partial Regex CodecsPattern();
}

public sealed record HlsManifestResult
{
    public IReadOnlyList<HlsVariant> Variants { get; init; } = [];
}

public sealed record HlsVariant
{
    public int Bandwidth { get; init; }
    public string? Resolution { get; init; }
    public string? Codecs { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "HLS URIs are relative paths, not absolute URIs")]
    public string? Uri { get; init; }
}
