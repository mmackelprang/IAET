namespace Iaet.Core.Models;

public sealed record AuthChain
{
    public required string Name { get; init; }
    public required IReadOnlyList<AuthChainStep> Steps { get; init; }
}
