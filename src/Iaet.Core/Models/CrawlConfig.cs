namespace Iaet.Core.Models;

public sealed record CrawlConfig
{
    public int MaxDepth { get; init; } = 3;
    public int MaxPages { get; init; } = 50;
    public IReadOnlyList<string> Blacklist { get; init; } = [];
    public IReadOnlyList<string> ExcludeSelectors { get; init; } = [];
}
