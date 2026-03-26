using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Crawler;
using NSubstitute;

namespace Iaet.Crawler.Tests;

public class PageInteractorTests
{
    private static DiscoveredElement LinkElement(string selector, string href) =>
        new() { TagName = "a", Selector = selector, Href = href };

    private static DiscoveredElement ButtonElement(string selector) =>
        new() { TagName = "button", Selector = selector };

    [Fact]
    public async Task ClicksLinkElement_ResolvesUrlAndReturnsNavigatedTo()
    {
        var navigator = Substitute.For<IPageNavigator>();
        navigator.CurrentUrl.Returns("http://localhost/");
        var interactor = new PageInteractor(navigator);
        var element = LinkElement("a[href]", "/about");

        var result = await interactor.InteractAsync(element);

        result.NavigatedTo.Should().Be("http://localhost/about");
        result.UrlChanged.Should().BeTrue();
    }

    [Fact]
    public async Task ClicksButton_TracksUrlChange_WhenUrlChangesAfterClick()
    {
        var navigator = Substitute.For<IPageNavigator>();
        navigator.CurrentUrl.Returns("http://localhost/page1");
        navigator.ClickAndWaitAsync("button:not([disabled])", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("http://localhost/page2"));
        var interactor = new PageInteractor(navigator);
        var element = ButtonElement("button:not([disabled])");

        var result = await interactor.InteractAsync(element);

        result.NavigatedTo.Should().Be("http://localhost/page2");
        result.UrlChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Button_NoNavigation_WhenUrlUnchangedAfterClick()
    {
        var navigator = Substitute.For<IPageNavigator>();
        navigator.CurrentUrl.Returns("http://localhost/page1");
        navigator.ClickAndWaitAsync("button:not([disabled])", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("http://localhost/page1"));
        var interactor = new PageInteractor(navigator);
        var element = ButtonElement("button:not([disabled])");

        var result = await interactor.InteractAsync(element);

        result.NavigatedTo.Should().BeNull();
        result.UrlChanged.Should().BeFalse();
    }
}
