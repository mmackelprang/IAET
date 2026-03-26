using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Crawler;

namespace Iaet.Crawler.Tests;

// ---------------------------------------------------------------------------
// Fake helpers
// ---------------------------------------------------------------------------

internal sealed class FakePageNavigator : IPageNavigator
{
    private readonly Dictionary<string, FakePage> _pages;

    public string CurrentUrl { get; private set; } = "";

    public FakePageNavigator(IEnumerable<FakePage> pages)
    {
        _pages = pages.ToDictionary(p => p.Url, StringComparer.OrdinalIgnoreCase);
    }

    public Task<IElementQueryable> NavigateAsync(string url, CancellationToken ct)
    {
        CurrentUrl = url;
        var page = _pages.GetValueOrDefault(url) ?? new FakePage(url, []);
        return Task.FromResult<IElementQueryable>(new FakeElementQueryable(page.Elements));
    }

    public Task<IReadOnlyList<string>> GetApiCallsSinceLastNavigationAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<string> ClickAndWaitAsync(string selector, CancellationToken ct)
        => Task.FromResult(CurrentUrl); // No SPA navigation in test fakes
}

internal sealed record FakePage(string Url, IReadOnlyList<ElementInfo> Elements);

internal sealed class FakeElementQueryable : IElementQueryable
{
    private readonly IReadOnlyList<ElementInfo> _elements;

    public FakeElementQueryable(IReadOnlyList<ElementInfo> elements) => _elements = elements;

    public Task<IReadOnlyList<ElementInfo>> QuerySelectorAllAsync(string selector, CancellationToken ct)
        => Task.FromResult(_elements);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class CrawlEngineTests
{
    private static ElementInfo LinkTo(string href) =>
        new("a", "a[href]", "Link", href);

    [Fact]
    public async Task VisitsStartPage_ReportHasOnePage()
    {
        var options = new CrawlOptions { StartUrl = "http://localhost/" };
        var navigator = new FakePageNavigator([new FakePage("http://localhost/", [])]);
        var engine = new CrawlEngine(options, navigator);

        var report = await engine.RunAsync();

        report.Pages.Should().HaveCount(1);
        report.Pages[0].Url.Should().Be("http://localhost/");
    }

    [Fact]
    public async Task FollowsLinks_BothStartAndLinkedPageVisited()
    {
        var page2Url = "http://localhost/page2";
        var options = new CrawlOptions { StartUrl = "http://localhost/" };
        var navigator = new FakePageNavigator(
        [
            new FakePage("http://localhost/", [LinkTo(page2Url)]),
            new FakePage(page2Url, [])
        ]);
        var engine = new CrawlEngine(options, navigator);

        var report = await engine.RunAsync();

        report.Pages.Should().HaveCount(2);
        report.Pages.Select(p => p.Url).Should().Contain(["http://localhost/", page2Url]);
    }

    [Fact]
    public async Task RespectsMaxDepth_OnlyVisitsStartPlusOneLevel()
    {
        var page2Url = "http://localhost/page2";
        var page3Url = "http://localhost/page3";
        var options = new CrawlOptions { StartUrl = "http://localhost/", MaxDepth = 1 };
        var navigator = new FakePageNavigator(
        [
            new FakePage("http://localhost/", [LinkTo(page2Url)]),
            new FakePage(page2Url, [LinkTo(page3Url)]),
            new FakePage(page3Url, [])
        ]);
        var engine = new CrawlEngine(options, navigator);

        var report = await engine.RunAsync();

        report.Pages.Should().HaveCount(2);
        report.Pages.Select(p => p.Url).Should().NotContain(page3Url);
    }

    [Fact]
    public async Task RespectsMaxPages_CapsAtConfiguredLimit()
    {
        var options = new CrawlOptions { StartUrl = "http://localhost/", MaxPages = 2 };
        var navigator = new FakePageNavigator(
        [
            new FakePage("http://localhost/",
            [
                LinkTo("http://localhost/page2"),
                LinkTo("http://localhost/page3")
            ]),
            new FakePage("http://localhost/page2", []),
            new FakePage("http://localhost/page3", [])
        ]);
        var engine = new CrawlEngine(options, navigator);

        var report = await engine.RunAsync();

        report.Pages.Should().HaveCount(2);
    }

    [Fact]
    public async Task RespectsUrlBlacklist_BlacklistedUrlNotVisited()
    {
        var blacklisted = "http://localhost/logout";
        var options = new CrawlOptions
        {
            StartUrl = "http://localhost/",
            UrlBlacklistPatterns = ["/logout"]
        };
        var navigator = new FakePageNavigator(
        [
            new FakePage("http://localhost/", [LinkTo(blacklisted)]),
            new FakePage(blacklisted, [])
        ]);
        var engine = new CrawlEngine(options, navigator);

        var report = await engine.RunAsync();

        report.Pages.Should().HaveCount(1);
        report.Pages[0].Url.Should().Be("http://localhost/");
    }

    [Fact]
    public async Task DoesNotRevisitPages_DuplicateLinksOnlyVisitedOnce()
    {
        var page2Url = "http://localhost/page2";
        var options = new CrawlOptions { StartUrl = "http://localhost/" };
        // Both page1 and page2 link to page2
        var navigator = new FakePageNavigator(
        [
            new FakePage("http://localhost/",
            [
                LinkTo(page2Url),
                LinkTo(page2Url)
            ]),
            new FakePage(page2Url, [LinkTo(page2Url)])
        ]);
        var engine = new CrawlEngine(options, navigator);

        var report = await engine.RunAsync();

        report.Pages.Should().HaveCount(2);
        report.Pages.Select(p => p.Url).Distinct().Should().HaveCount(2);
    }
}
