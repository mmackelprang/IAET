using Iaet.Crawler;
using Microsoft.Playwright;

namespace Iaet.Capture;

/// <summary>
/// Adapts a Playwright <see cref="IPage"/> to the <see cref="IElementQueryable"/>
/// contract used by the crawl engine.
/// </summary>
public sealed class PlaywrightElementQueryable : IElementQueryable
{
    private readonly IPage _page;

    public PlaywrightElementQueryable(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
    }

    public async Task<IReadOnlyList<ElementInfo>> QuerySelectorAllAsync(
        string selector, CancellationToken ct = default)
    {
        var handles = await _page.QuerySelectorAllAsync(selector).ConfigureAwait(false);
        var results = new List<ElementInfo>(handles.Count);

        foreach (var handle in handles)
        {
            var info = await _page.EvaluateAsync<ElementData>(
                @"(el) => ({
                    tagName: el.tagName.toLowerCase(),
                    text: (el.innerText || el.textContent || '').trim().substring(0, 200),
                    href: el.getAttribute('href'),
                    id: el.id,
                    className: el.className
                })",
                handle).ConfigureAwait(false);

            var cssSelector = BuildSelector(info?.TagName ?? "unknown", info?.Id, info?.ClassName);

            results.Add(new ElementInfo(
                TagName: info?.TagName ?? "unknown",
                Selector: cssSelector,
                Text: string.IsNullOrWhiteSpace(info?.Text) ? null : info!.Text,
                Href: info?.Href));
        }

        return results;
    }

    private static string BuildSelector(string tagName, string? id, string? className)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return $"#{id}";

        if (!string.IsNullOrWhiteSpace(className))
        {
            var firstClass = className!.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return $"{tagName}.{firstClass}";
        }

        return tagName;
    }

    /// <summary>Helper record for deserializing the JS evaluation result.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated by JSON deserialization in EvaluateAsync<T>.")]
    private sealed record ElementData(string TagName, string? Text, string? Href, string? Id, string? ClassName);
}
