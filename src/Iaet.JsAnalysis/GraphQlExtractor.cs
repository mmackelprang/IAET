using System.Text.RegularExpressions;

namespace Iaet.JsAnalysis;

public static partial class GraphQlExtractor
{
    public static IReadOnlyList<string> Extract(string jsContent)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<string>();
        foreach (Match match in QueryPattern().Matches(jsContent))
        {
            results.Add(match.Groups[1].Value);
        }

        foreach (Match match in MutationPattern().Matches(jsContent))
        {
            results.Add(match.Groups[1].Value);
        }

        return results;
    }

    [GeneratedRegex("""["'`](query\s+\w+[^"'`]*)["'`]""")]
    private static partial Regex QueryPattern();

    [GeneratedRegex("""["'`](mutation\s+\w+[^"'`]*)["'`]""")]
    private static partial Regex MutationPattern();
}
