namespace Iaet.Capture;

public sealed class StreamCaptureOptions
{
    public bool Enabled { get; init; }
    public bool CaptureSamples { get; init; }
    public int MaxFramesPerConnection { get; init; } = 1000;
    public int SampleDurationSeconds { get; init; } = 10;
    public int MaxMediaSegments { get; init; } = 3;
    public string? SampleOutputDirectory { get; init; }
}
