using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public static partial class FetchCallExtractor
{
    public static IReadOnlyList<ExtractedUrl> Extract(string jsContent, string sourceFile)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<ExtractedUrl>();
        var lines = jsContent.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            foreach (Match match in FetchPattern().Matches(line))
            {
                var url = match.Groups[1].Value;
                var method = "GET";
                var methodMatch = FetchMethodPattern().Match(line);
                if (methodMatch.Success)
                    method = methodMatch.Groups[1].Value.ToUpperInvariant();

                results.Add(new ExtractedUrl
                {
                    Url = url,
                    HttpMethod = method,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                });
            }

            foreach (Match match in XhrOpenPattern().Matches(line))
            {
                results.Add(new ExtractedUrl
                {
                    Url = match.Groups[2].Value,
                    HttpMethod = match.Groups[1].Value.ToUpperInvariant(),
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                });
            }
        }

        return results;
    }

    [GeneratedRegex("""fetch\(["'`]((?:/|https?://)[^"'`]+)["'`]""")]
    private static partial Regex FetchPattern();

    [GeneratedRegex("""method\s*:\s*["'`](\w+)["'`]""")]
    private static partial Regex FetchMethodPattern();

    [GeneratedRegex("""\.open\(["'`](\w+)["'`]\s*,\s*["'`]((?:/|https?://)[^"'`]+)["'`]""")]
    private static partial Regex XhrOpenPattern();
}
