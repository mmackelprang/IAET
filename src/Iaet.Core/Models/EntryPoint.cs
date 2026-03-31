namespace Iaet.Core.Models;

public sealed record EntryPoint
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; init; }
    public required string Label { get; init; }
}
