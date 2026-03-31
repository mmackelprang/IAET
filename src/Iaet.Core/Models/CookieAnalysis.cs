namespace Iaet.Core.Models;

public sealed record CookieAnalysis
{
    public required string ProjectName { get; init; }
    public required int TotalCookies { get; init; }
    public IReadOnlyList<string> AuthCritical { get; init; } = [];
    public IReadOnlyDictionary<string, TimeSpan> ExpiringWithin { get; init; } = new Dictionary<string, TimeSpan>();
    public IReadOnlyList<string> RotationDetected { get; init; } = [];
}
