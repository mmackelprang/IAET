namespace Iaet.Core.Models;

public sealed record BleDataFlow
{
    public required string CharacteristicUuid { get; init; }
    public string? CallbackLocation { get; init; }
    public string? ParsingDescription { get; init; }
    public string? VariableName { get; init; }
    public string? UiBinding { get; init; }
    public string? InferredMeaning { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Low;
}
