namespace Iaet.Core.Models;

public sealed record RequestDependency
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Reason { get; init; }
    public string? SharedField { get; init; }
}
