namespace Iaet.Core.Models;

public sealed record CaptureContext
{
    public required string Trigger { get; init; }
    public string? ElementSelector { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
