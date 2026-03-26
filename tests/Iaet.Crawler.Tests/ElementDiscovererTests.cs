using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Crawler;
using NSubstitute;

namespace Iaet.Crawler.Tests;

public class ElementDiscovererTests
{
    private static IElementQueryable MakeQueryable(params ElementInfo[] elements)
    {
        var queryable = Substitute.For<IElementQueryable>();
        queryable
            .QuerySelectorAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ElementInfo>>(elements));
        return queryable;
    }

    [Fact]
    public async Task FindsLinks_WhenQueryableReturnsAnchorElement()
    {
        var options = new CrawlOptions { StartUrl = "http://localhost/" };
        var element = new ElementInfo("a", "a[href]", "Home", "http://localhost/home");
        var queryable = MakeQueryable(element);
        var discoverer = new ElementDiscoverer(options);

        var results = await discoverer.DiscoverAsync(queryable);

        results.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DiscoveredElement
            {
                TagName = "a",
                Selector = "a[href]",
                Text = "Home",
                Href = "http://localhost/home"
            });
    }

    [Fact]
    public async Task FindsButtons_WhenQueryableReturnsButtonElement()
    {
        var options = new CrawlOptions { StartUrl = "http://localhost/" };
        var element = new ElementInfo("button", "button:not([disabled])", "Submit", null);
        var queryable = MakeQueryable(element);
        var discoverer = new ElementDiscoverer(options);

        var results = await discoverer.DiscoverAsync(queryable);

        results.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DiscoveredElement
            {
                TagName = "button",
                Selector = "button:not([disabled])",
                Text = "Submit",
                Href = null
            });
    }

    [Fact]
    public async Task ExcludesMatchingSelectors_WhenSelectorMatchesExcludedPattern()
    {
        var options = new CrawlOptions
        {
            StartUrl = "http://localhost/",
            ExcludedSelectors = ["nav-menu"]
        };
        var included = new ElementInfo("a", "a[href]", "Home", "/home");
        var excluded = new ElementInfo("button", "#main-nav-menu", "Nav", null);
        var queryable = MakeQueryable(included, excluded);
        var discoverer = new ElementDiscoverer(options);

        var results = await discoverer.DiscoverAsync(queryable);

        results.Should().ContainSingle()
            .Which.Selector.Should().Be("a[href]");
    }
}
