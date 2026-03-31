using System.Text.RegularExpressions;

namespace Iaet.Android.Extractors;

public static partial class ApkAuthExtractor
{
    public static IReadOnlyList<AuthEntry> Extract(string javaSource, string sourceFile)
    {
        if (string.IsNullOrEmpty(javaSource))
            return [];

        var results = new List<AuthEntry>();
        var lines = javaSource.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            // API key constants: static final String KEY_NAME = "value"
            foreach (Match match in ApiKeyConstantPattern().Matches(line))
            {
                results.Add(new AuthEntry
                {
                    Key = match.Groups[1].Value,
                    Value = match.Groups[2].Value,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    PatternType = "constant",
                });
            }

            // Google API keys (AIza...) even in obfuscated code
            foreach (Match match in GoogleApiKeyPattern().Matches(line))
            {
                var keyValue = match.Groups[1].Value;
                if (!results.Any(r => r.Value == keyValue))
                {
                    results.Add(new AuthEntry
                    {
                        Key = "GOOGLE_API_KEY",
                        Value = keyValue,
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        PatternType = "google-api-key",
                    });
                }
            }

            // Header construction: addHeader("Name", ...)
            foreach (Match match in HeaderPattern().Matches(line))
            {
                var headerName = match.Groups[1].Value;
                if (headerName.Contains("auth", StringComparison.OrdinalIgnoreCase)
                    || headerName.Contains("api-key", StringComparison.OrdinalIgnoreCase)
                    || headerName.Contains("token", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new AuthEntry
                    {
                        Key = headerName,
                        Value = "[dynamic]",
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        PatternType = "header",
                    });
                }
            }
        }

        return results;
    }

    [GeneratedRegex(@"(?:static\s+)?(?:final\s+)?String\s+(\w*(?:KEY|SECRET|TOKEN|CLIENT_ID|API)\w*)\s*=\s*""([^""]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyConstantPattern();

    [GeneratedRegex(@"""(AIza[A-Za-z0-9_-]{33,39})")]
    private static partial Regex GoogleApiKeyPattern();

    [GeneratedRegex(@"(?:addHeader|header)\(""([^""]+)")]
    private static partial Regex HeaderPattern();
}
