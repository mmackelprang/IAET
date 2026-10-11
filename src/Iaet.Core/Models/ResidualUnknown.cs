namespace Iaet.Core.Models;

/// <summary>
/// A question the investigation could not answer yet. Unknowns are tracked explicitly in
/// <c>knowledge/unknowns.json</c> so the Lead Investigator can plan rounds against them
/// instead of letting gaps disappear into prose.
/// </summary>
public sealed record ResidualUnknown
{
    /// <summary>Stable identifier (slug) so the same unknown is not recorded twice.</summary>
    public required string Id { get; init; }

    /// <summary>The open question, phrased so a human or agent can act on it.</summary>
    public required string Question { get; init; }

    /// <summary>Which analyzer or agent raised the unknown.</summary>
    public required string Source { get; init; }

    /// <summary>Artifact or location the unknown relates to.</summary>
    public string? Context { get; init; }

    /// <summary>Concrete next step that would resolve it (a CLI command, a capture, a device interaction).</summary>
    public string? SuggestedAction { get; init; }

    public UnknownStatus Status { get; init; } = UnknownStatus.Open;

    public DateTimeOffset RaisedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum UnknownStatus
{
    Open,
    Resolved,
    WontFix,
}
