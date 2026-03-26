namespace Iaet.Crawler;

public interface IElementQueryable
{
    Task<IReadOnlyList<ElementInfo>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);
}

public sealed record ElementInfo(string TagName, string Selector, string? Text, string? Href);
