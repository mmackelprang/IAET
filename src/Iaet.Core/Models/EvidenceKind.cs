namespace Iaet.Core.Models;

/// <summary>
/// Classifies how a finding was established. Borrowed from the "evidence over assertion"
/// discipline: observations, derivations, inferences and unknowns must never be conflated.
/// </summary>
public enum EvidenceKind
{
    /// <summary>Directly observed in an artifact or at runtime (bytes, captured traffic, a symbol table).</summary>
    Observation,

    /// <summary>Mechanically derived from observations (a parsed header, a decoded JNI name, a decompiled function).</summary>
    Derivation,

    /// <summary>Inferred by heuristics or pattern matching (a library fingerprint, a predicted endpoint).</summary>
    Inference,

    /// <summary>Could not be established; tracked so it is never silently assumed.</summary>
    Unknown,
}
