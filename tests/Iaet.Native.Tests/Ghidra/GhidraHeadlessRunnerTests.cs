using FluentAssertions;
using Iaet.Native.Ghidra;

namespace Iaet.Native.Tests.Ghidra;

public sealed class GhidraHeadlessRunnerTests
{
    [Fact]
    public void BuildArguments_produces_headless_invocation_with_post_script()
    {
        var args = GhidraHeadlessRunner.BuildArguments("/work/proj", "iaet", "/bin/libx.so", "/work/scripts", "/work/out.json",
            new GhidraRunOptions { Decompile = true, MaxFunctions = 500, AnalysisTimeoutSeconds = 120 });

        args.Should().StartWith(["/work/proj", "iaet", "-import", "/bin/libx.so", "-deleteProject"]);
        args.Should().ContainInOrder("-analysisTimeoutPerFile", "120");
        args.Should().ContainInOrder("-scriptPath", "/work/scripts");
        args.Should().ContainInOrder("-postScript", GhidraHeadlessRunner.ScriptFileName, "/work/out.json", "decompile=true", "maxFunctions=500");
        args.Should().NotContain("-processor");
    }

    [Fact]
    public void BuildArguments_adds_processor_when_language_given()
    {
        var args = GhidraHeadlessRunner.BuildArguments("p", "n", "b", "s", "o", new GhidraRunOptions { LanguageId = "ARM:LE:32:v8" });

        args.Should().ContainInOrder("-processor", "ARM:LE:32:v8");
    }

    [Fact]
    public async Task MaterializeScript_writes_embedded_java_source()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = await GhidraHeadlessRunner.MaterializeScriptAsync(dir);

            File.Exists(path).Should().BeTrue();
            var src = await File.ReadAllTextAsync(path);
            src.Should().Contain("class IaetGhidraExport extends GhidraScript");
            src.Should().Contain($"SCRIPT_VERSION = \"{GhidraHeadlessRunner.ScriptVersion}\"");
            src.Should().Contain("getScriptArgs()");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ParameterKey_changes_with_options()
    {
        var a = new GhidraRunOptions { Decompile = false }.ParameterKey();
        var b = new GhidraRunOptions { Decompile = true }.ParameterKey();

        a.Should().NotBe(b);
    }
}
