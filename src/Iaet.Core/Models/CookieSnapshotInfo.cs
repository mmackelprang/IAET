namespace Iaet.Core.Models;

public sealed record CookieSnapshotInfo
{
    public required Guid Id { get; init; }
    public required string ProjectName { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required string Source { get; init; }
    public required IReadOnlyList<CapturedCookie> Cookies { get; init; }
}
