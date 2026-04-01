namespace Iaet.Core.Models;

public sealed record BleService
{
    public required string Uuid { get; init; }
    public string? Name { get; init; }
    public bool IsStandardService { get; init; }
    public IReadOnlyList<BleCharacteristic> Characteristics { get; init; } = [];
    public string? SourceFile { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
}
