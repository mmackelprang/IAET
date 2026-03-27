using Iaet.Crawler;
using Microsoft.Playwright;

namespace Iaet.Capture;

/// <summary>
/// Bridges a live Playwright <see cref="IPage"/> to the
/// <see cref="IPageNavigator"/> interface consumed by <see cref="CrawlEngine"/>.
/// </summary>
public sealed class PlaywrightPageNavigator : IPageNavigator
{
    private readonly IPage _page;

    public PlaywrightPageNavigator(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
    }

    /// <inheritdoc/>
    public string CurrentUrl => _page.Url;

    /// <inheritdoc/>
    public async Task<IElementQueryable> NavigateAsync(string url, CancellationToken ct = default)
    {
        await _page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        }).ConfigureAwait(false);

        return new PlaywrightElementQueryable(_page);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetApiCallsSinceLastNavigationAsync(CancellationToken ct = default)
    {
        // API calls are captured separately by CdpNetworkListener.
        // Return an empty list so the crawl engine doesn't double-count.
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <inheritdoc/>
    public async Task<string> ClickAndWaitAsync(string selector, CancellationToken ct = default)
    {
        await _page.ClickAsync(selector).ConfigureAwait(false);

        try
        {
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 5000 }).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Some clicks don't trigger navigation; that's expected.
        }

        return _page.Url;
    }
}
