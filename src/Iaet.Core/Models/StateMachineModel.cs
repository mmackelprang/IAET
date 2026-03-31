namespace Iaet.Core.Models;

public sealed record StateMachineModel
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> States { get; init; }
    public required IReadOnlyList<StateTransition> Transitions { get; init; }
    public required string InitialState { get; init; }
}
