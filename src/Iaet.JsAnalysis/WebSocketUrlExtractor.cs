using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public static partial class WebSocketUrlExtractor
{
    public static IReadOnlyList<ExtractedUrl> Extract(string jsContent, string sourceFile)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<ExtractedUrl>();
        var lines = jsContent.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            foreach (Match match in WsConstructorPattern().Matches(lines[lineIdx]))
            {
                results.Add(new ExtractedUrl
                {
                    Url = match.Groups[1].Value,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                    Context = "WebSocket constructor",
                });
            }
        }

        return results;
    }

    [GeneratedRegex("""new\s+WebSocket\(["'`](wss?://[^"'`]+)["'`]""")]
    private static partial Regex WsConstructorPattern();
}
