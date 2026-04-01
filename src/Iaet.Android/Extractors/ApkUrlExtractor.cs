using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

public static partial class ApkUrlExtractor
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp",
        ".css", ".woff", ".woff2", ".ttf", ".eot", ".mp3", ".mp4",
    };

    public static IReadOnlyList<ExtractedUrl> Extract(string javaSource, string sourceFile)
    {
        if (string.IsNullOrEmpty(javaSource))
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ExtractedUrl>();
        var lines = javaSource.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            // Retrofit annotations: @GET("path"), @POST("path"), etc.
            foreach (Match match in RetrofitPattern().Matches(line))
            {
                var method = match.Groups[1].Value.ToUpperInvariant();
                var path = match.Groups[2].Value;
                if (seen.Add($"{method}:{path}"))
                {
                    results.Add(new ExtractedUrl
                    {
                        Url = path,
                        HttpMethod = method,
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        Confidence = ConfidenceLevel.High,
                        Context = "Retrofit annotation",
                    });
                }
            }

            // String literal URLs (http/https/wss)
            foreach (Match match in UrlStringPattern().Matches(line))
            {
                var url = match.Groups[1].Value;
                if (IsIgnoredUrl(url)) continue;
                if (!seen.Add(url)) continue;

                results.Add(new ExtractedUrl
                {
                    Url = url,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                    Context = line.Trim().Length > 120 ? line.Trim()[..120] : line.Trim(),
                });
            }
        }

        return results;
    }

    public static IReadOnlyList<ExtractedUrl> ExtractFromDirectory(string decompiledDir)
    {
        ArgumentNullException.ThrowIfNull(decompiledDir);

        if (!Directory.Exists(decompiledDir))
            return [];

        var allUrls = new List<ExtractedUrl>();

        foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(decompiledDir, file);
            allUrls.AddRange(Extract(source, relativePath));
        }

        // Deduplicate across files (keep first occurrence)
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return allUrls.Where(u => seen.Add(u.Url)).ToList();
    }

    private static bool IsIgnoredUrl(string url)
    {
        foreach (var ext in IgnoredExtensions)
        {
            if (url.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    [GeneratedRegex("""@(GET|POST|PUT|DELETE|PATCH|HEAD)\("([^"]+)"\)""")]
    private static partial Regex RetrofitPattern();

    [GeneratedRegex(""""((?:https?://|wss?://)[^"]+)"""")]
    private static partial Regex UrlStringPattern();
}
