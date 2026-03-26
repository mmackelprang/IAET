using Iaet.Core.Models;

namespace Iaet.Crawler;

public sealed class ElementDiscoverer
{
    private static readonly string[] Selectors =
    [
        "a[href]",
        "button:not([disabled])",
        "[role='button']",
        "input[type='submit']",
        "[onclick]"
    ];

    public static async Task<IReadOnlyList<DiscoveredElement>> DiscoverAsync(
        IElementQueryable queryable,
        CrawlOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(options);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<DiscoveredElement>();

        foreach (var selector in Selectors)
        {
            var elements = await queryable.QuerySelectorAllAsync(selector, ct).ConfigureAwait(false);
            foreach (var el in elements)
            {
                if (!seen.Add(el.Selector))
                    continue;

                if (options.IsSelectorExcluded(el.Selector))
                    continue;

                results.Add(new DiscoveredElement
                {
                    TagName = el.TagName,
                    Selector = el.Selector,
                    Text = el.Text,
                    Href = el.Href
                });
            }
        }

        return results;
    }
}
