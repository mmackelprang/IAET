namespace Iaet.Core.Models;

/// <summary>
/// Result of probing one external tool that IAET depends on (jadx, Ghidra, Playwright, ...).
/// </summary>
public sealed record ToolHealth
{
    public required string Name { get; init; }

    /// <summary>Which IAET capabilities need this tool (e.g. "apk decompile", "native analyze --decompile").</summary>
    public required IReadOnlyList<string> Capabilities { get; init; }

    public required bool Available { get; init; }

    /// <summary>Resolved executable path or installation directory when available.</summary>
    public string? ResolvedPath { get; init; }

    public string? Version { get; init; }

    /// <summary>Human-readable detail: why the probe failed, or how to install the tool.</summary>
    public string? Message { get; init; }

    /// <summary>True when IAET's core workflows work without this tool.</summary>
    public bool Optional { get; init; } = true;
}
