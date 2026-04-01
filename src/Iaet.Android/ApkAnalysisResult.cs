using Iaet.Android.Extractors;
using Iaet.Core.Models;

namespace Iaet.Android;

public sealed record ApkAnalysisResult
{
    public required ApkInfo Manifest { get; init; }
    public required NetworkSecurityConfig NetworkSecurity { get; init; }
    public required IReadOnlyList<ExtractedUrl> Urls { get; init; }
    public required IReadOnlyList<AuthEntry> AuthEntries { get; init; }
    public string? DecompiledPath { get; init; }
    public int JavaFileCount { get; init; }
}
