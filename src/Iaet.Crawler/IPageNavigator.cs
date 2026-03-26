using System.Diagnostics.CodeAnalysis;

namespace Iaet.Crawler;

public interface IPageNavigator
{
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    Task<IElementQueryable> NavigateAsync(string url, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetApiCallsSinceLastNavigationAsync(CancellationToken ct = default);

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    Task<string> ClickAndWaitAsync(string selector, CancellationToken ct = default);

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    string CurrentUrl { get; }
}
