namespace Iaet.Core.Models;

public sealed record CookieChange
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string OldValue { get; init; }
    public required string NewValue { get; init; }
}
