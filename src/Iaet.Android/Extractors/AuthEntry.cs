namespace Iaet.Android.Extractors;

public sealed record AuthEntry
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string SourceFile { get; init; }
    public int? LineNumber { get; init; }
    public required string PatternType { get; init; }
}
