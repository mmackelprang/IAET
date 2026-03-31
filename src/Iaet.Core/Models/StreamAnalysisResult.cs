namespace Iaet.Core.Models;

public sealed record StreamAnalysisResult
{
    public required Guid StreamId { get; init; }
    public required StreamProtocol Protocol { get; init; }
    public IReadOnlyList<string> MessageTypes { get; init; } = [];
    public string? SubProtocol { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public StateMachineModel? StateMachine { get; init; }
}
