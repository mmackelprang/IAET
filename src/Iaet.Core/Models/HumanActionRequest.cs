namespace Iaet.Core.Models;

public sealed record HumanActionRequest
{
    public required string Action { get; init; }
    public required string Reason { get; init; }
    public string Urgency { get; init; } = "normal";
}
