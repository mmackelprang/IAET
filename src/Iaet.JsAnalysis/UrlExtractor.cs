using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public static partial class UrlExtractor
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2",
        ".ttf", ".eot", ".mp3", ".mp4", ".webm", ".webp", ".map",
    };

    public static IReadOnlyList<ExtractedUrl> Extract(string jsContent, string sourceFile)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ExtractedUrl>();
        var lines = jsContent.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            foreach (Match match in UrlPattern().Matches(line))
            {
                var url = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                if (IsIgnoredUrl(url))
                    continue;

                if (!seen.Add(url))
                    continue;

                results.Add(new ExtractedUrl
                {
                    Url = url,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Context = line.Trim().Length > 120 ? line.Trim()[..120] : line.Trim(),
                    Confidence = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? ConfidenceLevel.High
                        : ConfidenceLevel.Medium,
                });
            }
        }

        return results;
    }

    private static bool IsIgnoredUrl(string url)
    {
        if (!url.StartsWith('/') && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var ext in IgnoredExtensions)
        {
            if (url.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    [GeneratedRegex("""["'`]((?:https?://[^\s"'`]+)|(?:/[a-zA-Z][a-zA-Z0-9_/\-.{}*]+))["'`]""")]
    private static partial Regex UrlPattern();
}
