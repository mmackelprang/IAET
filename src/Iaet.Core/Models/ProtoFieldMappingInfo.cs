namespace Iaet.Core.Models;

/// <summary>
/// Lightweight record carrying a protobuf field mapping recovered from decompiled source.
/// Used to pass proto field name evidence across project boundaries without a
/// direct reference to <c>Iaet.Android</c>.
/// </summary>
/// <remarks>
/// Positions are 0-based (proto field numbers are 1-based; callers must subtract 1).
/// Confidence is represented as <see cref="ConfidenceLevel"/>: High for field-number
/// constants and getter methods, Medium/Low for positional-access heuristics.
/// </remarks>
public sealed record ProtoFieldMappingInfo
{
    /// <summary>0-based array position in the protojson array.</summary>
    public required int Position { get; init; }

    /// <summary>Human-readable camelCase field name inferred from source patterns.</summary>
    public required string SuggestedName { get; init; }

    /// <summary>
    /// Source evidence category: "field_constant", "getter", "descriptor", "position_access".
    /// </summary>
    public required string Source { get; init; }

    /// <summary>Confidence level of this mapping.</summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
}
