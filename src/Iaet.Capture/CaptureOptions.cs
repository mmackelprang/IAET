namespace Iaet.Capture;

public sealed class CaptureOptions
{
    public required string TargetApplication { get; init; }
    public string? Profile { get; init; }
    public bool Headless { get; init; }
}
