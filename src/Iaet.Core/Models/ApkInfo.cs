namespace Iaet.Core.Models;

public sealed record ApkInfo
{
    public required string PackageName { get; init; }
    public string? VersionName { get; init; }
    public int? VersionCode { get; init; }
    public int? MinSdk { get; init; }
    public int? TargetSdk { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public IReadOnlyList<string> ExportedServices { get; init; } = [];
    public IReadOnlyList<string> ExportedReceivers { get; init; } = [];
    public IReadOnlyList<string> ExportedProviders { get; init; } = [];
}
