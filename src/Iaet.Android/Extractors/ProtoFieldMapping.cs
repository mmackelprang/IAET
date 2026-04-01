using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

/// <summary>
/// A mapping from a protobuf array position to a human-readable field name,
/// recovered from decompiled Java source code patterns.
/// </summary>
public sealed record ProtoFieldMapping
{
    public required int Position { get; init; }
    public required string SuggestedName { get; init; }
    public required string Source { get; init; } // "field_constant", "getter", "descriptor", "position_access"
    public required string SourceFile { get; init; }
    public int? LineNumber { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
}
