namespace Iaet.Core.Models;

public sealed record RoundPlan
{
    public required int RoundNumber { get; init; }
    public required string Rationale { get; init; }
    public IReadOnlyList<AgentDispatch> Dispatches { get; init; } = [];
    public IReadOnlyList<HumanActionRequest> HumanActions { get; init; } = [];
}
