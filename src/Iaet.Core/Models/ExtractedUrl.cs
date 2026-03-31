namespace Iaet.Core.Models;

public sealed record ExtractedUrl
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; init; }
    public string? HttpMethod { get; init; }
    public string? SourceFile { get; init; }
    public int? LineNumber { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
    public string? Context { get; init; }
}
