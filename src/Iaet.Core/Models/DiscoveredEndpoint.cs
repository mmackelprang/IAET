namespace Iaet.Core.Models;

public sealed record DiscoveredEndpoint
{
    public required string Signature { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public int ObservationCount { get; init; }
    public IReadOnlyList<string> Sources { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
}
