namespace Iaet.Core.Models;

public sealed record Annotation(
    string? HumanName,
    string? Description,
    IReadOnlyList<string> Tags,
    StabilityRating Stability,
    bool IsDestructive
);

public enum StabilityRating { Unknown, Stable, Unstable, Deprecated }
