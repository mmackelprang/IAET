using System.Diagnostics.CodeAnalysis;
using Iaet.Core.Models;

namespace Iaet.Crawler;

public sealed record InteractionResult(
    [property: SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    string? NavigatedTo,
    bool UrlChanged);

public sealed class PageInteractor
{
    private readonly IPageNavigator _navigator;

    public PageInteractor(IPageNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        _navigator = navigator;
    }

    public async Task<InteractionResult> InteractAsync(DiscoveredElement element, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element.Href is not null)
        {
            var targetUrl = ResolveUrl(_navigator.CurrentUrl, element.Href);
            return new InteractionResult(targetUrl, true);
        }

        var urlBefore = _navigator.CurrentUrl;
        var urlAfter = await _navigator.ClickAndWaitAsync(element.Selector, ct).ConfigureAwait(false);
        var navigated = !string.Equals(urlBefore, urlAfter, StringComparison.OrdinalIgnoreCase);
        return new InteractionResult(navigated ? urlAfter : null, navigated);
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    private static string ResolveUrl(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var abs) && abs.Scheme != Uri.UriSchemeFile)
            return abs.ToString();
        return new Uri(new Uri(baseUrl), href).ToString();
    }
}
