using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public sealed record BundleAnalysisResult
{
    public required string SourceFile { get; init; }
    public IReadOnlyList<ExtractedUrl> Urls { get; init; } = [];
    public IReadOnlyList<ExtractedUrl> FetchCalls { get; init; } = [];
    public IReadOnlyList<ExtractedUrl> WebSocketUrls { get; init; } = [];
    public IReadOnlyList<string> GraphQlQueries { get; init; } = [];
    public IReadOnlyList<KeyValuePair<string, string>> ConfigEntries { get; init; } = [];
    public IReadOnlyList<string> GoDeeper { get; init; } = [];
}
