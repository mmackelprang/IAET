namespace Iaet.Core.Models;

public sealed record AuthChainStep
{
    public required string Endpoint { get; init; }
    public string? Provides { get; init; }
    public string? Consumes { get; init; }
    public required string Type { get; init; }
}
