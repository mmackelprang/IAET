namespace Iaet.Core.Models;

public sealed record AgentFindings
{
    public required string Agent { get; init; }
    public required int RoundNumber { get; init; }
    public IReadOnlyList<DiscoveredEndpoint> Endpoints { get; init; } = [];
    public IReadOnlyList<string> GoDeeper { get; init; } = [];
    public IReadOnlyList<HumanActionRequest> HumanActions { get; init; } = [];
}
