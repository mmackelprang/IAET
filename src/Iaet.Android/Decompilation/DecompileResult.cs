namespace Iaet.Android.Decompilation;

public sealed record DecompileResult
{
    public required bool Success { get; init; }
    public required string OutputDirectory { get; init; }
    public required string Tool { get; init; }
    public int FileCount { get; init; }
    public long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}
