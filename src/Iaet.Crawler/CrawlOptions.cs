using System.Diagnostics.CodeAnalysis;

namespace Iaet.Crawler;

public sealed class CrawlOptions
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    public required string StartUrl { get; init; }

    public string TargetApplication { get; init; } = "Unknown";
    public int MaxDepth { get; init; } = 3;
    public int MaxPages { get; init; } = 50;
    public int MaxDurationSeconds { get; init; } = 300;
    public bool Headless { get; init; }
    public IReadOnlyList<string> UrlWhitelistPatterns { get; init; } = [];
    public IReadOnlyList<string> UrlBlacklistPatterns { get; init; } = [];
    public IReadOnlyList<string> ExcludedSelectors { get; init; } = [];
    public FormFillStrategy FormStrategy { get; init; } = FormFillStrategy.Skip;
    public bool CaptureStreams { get; init; } = true;

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    public bool IsUrlAllowed(string url)
    {
        var path = new Uri(url).AbsolutePath;
        if (UrlBlacklistPatterns.Count > 0 &&
            UrlBlacklistPatterns.Any(p => MatchesPattern(path, p)))
            return false;
        if (UrlWhitelistPatterns.Count > 0)
            return UrlWhitelistPatterns.Any(p => MatchesPattern(path, p));
        return true;
    }

    public bool IsSelectorExcluded(string selector) =>
        ExcludedSelectors.Any(excluded =>
            selector.Contains(excluded, StringComparison.Ordinal) ||
            excluded.Contains(selector, StringComparison.Ordinal));

    private static bool MatchesPattern(string path, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(path, regex);
    }
}

public enum FormFillStrategy { Skip, FillWithTestData }
