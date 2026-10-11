using FluentAssertions;
using Iaet.Native.Ghidra;

namespace Iaet.Native.Tests.Ghidra;

public sealed class GhidraLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "iaet-ghidra-" + Guid.NewGuid().ToString("N"));

    public GhidraLocatorTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "support"));
        File.WriteAllText(Path.Combine(_root, "support", "analyzeHeadless"), "#!/bin/sh\n");
        File.WriteAllText(Path.Combine(_root, "support", "analyzeHeadless.bat"), "@echo off\n");
    }

    [Fact]
    public void ResolveLauncher_accepts_install_root()
    {
        var launcher = GhidraLocator.ResolveLauncher(_root);

        launcher.Should().NotBeNull();
        Path.GetFileName(launcher!).Should().StartWith("analyzeHeadless");
    }

    [Fact]
    public void ResolveLauncher_accepts_support_dir_and_file()
    {
        GhidraLocator.ResolveLauncher(Path.Combine(_root, "support")).Should().NotBeNull();
        GhidraLocator.ResolveLauncher(Path.Combine(_root, "support", "analyzeHeadless")).Should().EndWith("analyzeHeadless");
    }

    [Fact]
    public void FindAnalyzeHeadless_uses_environment_variables_in_order()
    {
        var env = new Dictionary<string, string?> { ["GHIDRA_INSTALL_DIR"] = _root };

        var launcher = GhidraLocator.FindAnalyzeHeadless(explicitPath: null, getEnv: k => env.GetValueOrDefault(k), pathVariable: string.Empty);

        launcher.Should().NotBeNull();
        launcher!.Should().StartWith(_root);
    }

    [Fact]
    public void FindAnalyzeHeadless_searches_path_variable()
    {
        var launcher = GhidraLocator.FindAnalyzeHeadless(explicitPath: null, getEnv: _ => null, pathVariable: Path.Combine(_root, "support"));

        launcher.Should().NotBeNull();
    }

    [Fact]
    public void FindAnalyzeHeadless_returns_null_when_absent()
    {
        GhidraLocator.FindAnalyzeHeadless(explicitPath: null, getEnv: _ => null, pathVariable: "/definitely/not/here").Should().BeNull();
        GhidraLocator.FindAnalyzeHeadless(explicitPath: "/nope/ghidra", getEnv: _ => null, pathVariable: string.Empty).Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
