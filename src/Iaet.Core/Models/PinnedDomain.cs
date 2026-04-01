namespace Iaet.Core.Models;

public sealed record PinnedDomain
{
    public required string Domain { get; init; }
    public IReadOnlyList<string> Pins { get; init; } = [];
}
