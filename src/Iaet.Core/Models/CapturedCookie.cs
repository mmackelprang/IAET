namespace Iaet.Core.Models;

public sealed record CapturedCookie
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string Path { get; init; }
    public required string Value { get; init; }
    public DateTimeOffset? Expires { get; init; }
    public bool HttpOnly { get; init; }
    public bool Secure { get; init; }
    public string? SameSite { get; init; }
    public long Size { get; init; }
}
