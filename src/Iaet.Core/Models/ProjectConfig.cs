namespace Iaet.Core.Models;

public sealed record ProjectConfig
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required TargetType TargetType { get; init; }
    public required IReadOnlyList<EntryPoint> EntryPoints { get; init; }
    public bool AuthRequired { get; init; }
    public string? AuthMethod { get; init; }
    public IReadOnlyList<string> FocusAreas { get; init; } = [];
    public CrawlConfig? CrawlConfig { get; init; }
    public int CurrentRound { get; init; }
    public ProjectStatus Status { get; init; } = ProjectStatus.New;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; init; } = DateTimeOffset.UtcNow;
}
