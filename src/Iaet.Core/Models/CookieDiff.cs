namespace Iaet.Core.Models;

public sealed record CookieDiff
{
    public required Guid BeforeSnapshotId { get; init; }
    public required Guid AfterSnapshotId { get; init; }
    public IReadOnlyList<CapturedCookie> Added { get; init; } = [];
    public IReadOnlyList<CapturedCookie> Removed { get; init; } = [];
    public IReadOnlyList<CookieChange> Changed { get; init; } = [];
}
