using System.Text.RegularExpressions;

namespace Iaet.Explorer;

/// <summary>
/// Shared helper for URL path normalisation used by the API and Razor pages.
/// Replaces numeric IDs, hex tokens, and GUIDs with a <c>{id}</c> placeholder.
/// </summary>
internal static partial class PathNormalizer
{
    [GeneratedRegex(
        @"^(\d+|[0-9a-f]{8,}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdSegmentRegex();

    /// <summary>
    /// Normalises a URL by replacing ID-like path segments with <c>{id}</c>.
    /// Returns the original value unchanged when it is not an absolute URL.
    /// </summary>
    internal static string NormalizePath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join("/", segments.Select(s => IsId(s) ? "{id}" : s));
        return "/" + normalized;
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="segment"/> looks like an ID.</summary>
    internal static bool IsId(string segment) => IdSegmentRegex().IsMatch(segment);
}
