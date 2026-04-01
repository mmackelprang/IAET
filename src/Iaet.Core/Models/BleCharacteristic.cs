namespace Iaet.Core.Models;

public sealed record BleCharacteristic
{
    public required string Uuid { get; init; }
    public string? Name { get; init; }
    public IReadOnlyList<BleOperationType> Operations { get; init; } = [];
    public string? DataFormat { get; init; }
    public string? SourceFile { get; init; }
}
