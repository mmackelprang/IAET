namespace Iaet.Core.Models;

public sealed record StateTransition
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Trigger { get; init; }
}
