using FluentAssertions;
using Iaet.Crawler;

namespace Iaet.Crawler.Tests;

public class CrawlOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var options = new CrawlOptions { StartUrl = "http://localhost/" };

        options.MaxDepth.Should().Be(3);
        options.MaxPages.Should().Be(50);
        options.MaxDurationSeconds.Should().Be(300);
    }

    [Fact]
    public void IsUrlAllowed_NoPatterns_AllowsAll()
    {
        var options = new CrawlOptions { StartUrl = "http://localhost/" };

        options.IsUrlAllowed("http://localhost/any/path").Should().BeTrue();
        options.IsUrlAllowed("http://localhost/other").Should().BeTrue();
    }

    [Fact]
    public void IsUrlAllowed_WithWhitelist_RestrictsToPattern()
    {
        var options = new CrawlOptions
        {
            StartUrl = "http://localhost/",
            UrlWhitelistPatterns = ["/api/*"]
        };

        options.IsUrlAllowed("http://localhost/api/users").Should().BeTrue();
        options.IsUrlAllowed("http://localhost/admin/settings").Should().BeFalse();
    }

    [Fact]
    public void IsUrlAllowed_WithBlacklist_ExcludesPattern()
    {
        var options = new CrawlOptions
        {
            StartUrl = "http://localhost/",
            UrlBlacklistPatterns = ["/logout"]
        };

        options.IsUrlAllowed("http://localhost/logout").Should().BeFalse();
        options.IsUrlAllowed("http://localhost/home").Should().BeTrue();
    }

    [Fact]
    public void IsSelectorExcluded_MatchesSubstring()
    {
        var options = new CrawlOptions
        {
            StartUrl = "http://localhost/",
            ExcludedSelectors = ["nav-menu"]
        };

        // selector contains the excluded substring
        options.IsSelectorExcluded("#main-nav-menu").Should().BeTrue();
        // selector does NOT contain the excluded pattern (reversed direction is not checked)
        options.IsSelectorExcluded("nav").Should().BeFalse();
        // no match
        options.IsSelectorExcluded("#submit-btn").Should().BeFalse();
    }
}
