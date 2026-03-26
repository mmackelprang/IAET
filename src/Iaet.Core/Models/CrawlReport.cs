using System.Diagnostics.CodeAnalysis;

namespace Iaet.Core.Models;

public sealed class CrawlReport
{
    public required Guid SessionId { get; init; }
    public required string TargetApplication { get; init; }

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    public required string StartUrl { get; init; }

    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required IReadOnlyList<DiscoveredPage> Pages { get; init; }
    public required int TotalRequestsCaptured { get; init; }
    public required int TotalStreamsCaptured { get; init; }
}

public sealed class DiscoveredPage
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    public required string Url { get; init; }

    public required int Depth { get; init; }
    public required IReadOnlyList<DiscoveredElement> InteractiveElements { get; init; }
    public required IReadOnlyList<string> ApiCallsTriggered { get; init; }
    public required IReadOnlyList<string> NavigatedTo { get; init; }
}

public sealed class DiscoveredElement
{
    public required string TagName { get; init; }
    public required string Selector { get; init; }
    public string? Text { get; init; }
    public string? Href { get; init; }
    public bool WasInteracted { get; init; }
}
