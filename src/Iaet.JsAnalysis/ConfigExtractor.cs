using System.Text.RegularExpressions;

namespace Iaet.JsAnalysis;

public static partial class ConfigExtractor
{
    public static IReadOnlyList<KeyValuePair<string, string>> Extract(string jsContent)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ConstAssignmentPattern().Matches(jsContent))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (seen.Add(key))
                results.Add(new KeyValuePair<string, string>(key, value));
        }

        foreach (Match match in ObjectPropertyUrlPattern().Matches(jsContent))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (seen.Add(key))
                results.Add(new KeyValuePair<string, string>(key, value));
        }

        foreach (Match match in ConstBoolPattern().Matches(jsContent))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (seen.Add(key))
                results.Add(new KeyValuePair<string, string>(key, value));
        }

        return results;
    }

    [GeneratedRegex("""const\s+([A-Z][A-Z0-9_]+)\s*=\s*["'`](https?://[^"'`]+)["'`]""")]
    private static partial Regex ConstAssignmentPattern();

    [GeneratedRegex("""(\w+(?:Url|Api|Endpoint|Base))\s*:\s*["'`](https?://[^"'`]+)["'`]""", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectPropertyUrlPattern();

    [GeneratedRegex("""const\s+([A-Z][A-Z0-9_]+)\s*=\s*(true|false)""")]
    private static partial Regex ConstBoolPattern();
}
