namespace Iaet.Core.Models;

public sealed record AgentDispatch
{
    public required string Agent { get; init; }
    public IReadOnlyList<string> Targets { get; init; } = [];
    public IReadOnlyList<string> Actions { get; init; } = [];
}
