namespace Iaet.Core.Models;

/// <summary>
/// Provenance attached to a finding: which provider produced it, from which artifact,
/// at which location, how certain it is, and what it does not establish.
/// </summary>
public sealed record Evidence
{
    public required EvidenceKind Kind { get; init; }

    /// <summary>Tool or analyzer that produced the finding (e.g. "elf-reader", "ghidra", "jadx", "network-capture").</summary>
    public required string Provider { get; init; }

    /// <summary>Artifact the finding came from (file path relative to the project, URL, or session id).</summary>
    public string? Artifact { get; init; }

    /// <summary>SHA-256 of the artifact bytes when the artifact is a file, so findings can be tied to exact inputs.</summary>
    public string? ArtifactSha256 { get; init; }

    /// <summary>Location inside the artifact (file:line, 0x-prefixed address, byte offset, or symbol name).</summary>
    public string? Location { get; init; }

    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;

    /// <summary>What this evidence does not establish (e.g. "static only — not observed at runtime").</summary>
    public IReadOnlyList<string> Limitations { get; init; } = [];
}
