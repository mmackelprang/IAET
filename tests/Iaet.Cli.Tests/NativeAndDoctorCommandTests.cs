using FluentAssertions;
using Iaet.Cli.Commands;
using Iaet.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace Iaet.Cli.Tests;

public class NativeAndDoctorCommandTests
{
    private static ServiceProvider Services() => new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void Native_command_exposes_analyze_search_and_function()
    {
        var cmd = NativeCommand.Create(Services());

        cmd.Name.Should().Be("native");
        cmd.Subcommands.Select(c => c.Name).Should().BeEquivalentTo("analyze", "search", "function");
        cmd.Subcommands.Single(c => c.Name == "analyze").Options.Select(o => o.Name)
            .Should().Contain(["--project", "--binary", "--ghidra-path", "--no-ghidra", "--decompile", "--max-functions", "--no-cache"]);
    }

    [Fact]
    public void Doctor_command_has_json_option()
    {
        var cmd = DoctorCommand.Create(Services());

        cmd.Name.Should().Be("doctor");
        cmd.Options.Select(o => o.Name).Should().Contain("--json");
    }

    [Fact]
    public async Task Doctor_checks_always_include_dotnet_and_run_registered_providers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Iaet.Core.Abstractions.IToolProvider>(new FakeProvider());
        var results = await DoctorCommand.RunChecksAsync(services.BuildServiceProvider());

        results.Should().Contain(r => r.Name == "dotnet" && r.Available);
        results.Should().Contain(r => r.Name == "fake-engine" && !r.Available && r.Message == "install it");
    }

    [Fact]
    public void Search_matches_across_symbols_jni_urls_and_functions()
    {
        var binaries = new List<NativeBinaryInfo>
        {
            new()
            {
                Path = "native/libbridge.so",
                Sha256 = "x",
                Format = BinaryFormat.Elf,
                Exports = [new NativeSymbol { Name = "Java_com_app_Radio_setFrequency" }],
                Imports = [new NativeSymbol { Name = "SSL_write" }],
                JniBindings = [new JniBinding { Symbol = "Java_com_app_Radio_setFrequency", JavaClass = "com.app.Radio", JavaMethod = "setFrequency" }],
                Urls = [new ExtractedUrl { Url = "https://api.vendor.com/frequency" }],
                Functions = [new NativeFunction { Name = "encode_frequency", EntryAddress = "0x1000", ReferencedStrings = ["FREQ=%d"] }],
            },
        };

        var hits = NativeCommand.Search(binaries, "frequency").ToList();

        hits.Select(h => h.Kind).Should().BeEquivalentTo("export", "jni", "url", "function");
        NativeCommand.Search(binaries, "FREQ=").Should().ContainSingle(h => h.Kind == "string");
        NativeCommand.Search(binaries, "ssl").Should().ContainSingle(h => h.Kind == "import");
    }

    [Fact]
    public async Task MergeUnknowns_appends_new_and_keeps_existing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iaet-unknowns-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "unknowns.json");
        try
        {
            var first = await NativeCommand.MergeUnknownsAsync(path,
            [
                new ResidualUnknown { Id = "a", Question = "A?", Source = "test" },
                new ResidualUnknown { Id = "b", Question = "B?", Source = "test" },
            ], CancellationToken.None);
            var second = await NativeCommand.MergeUnknownsAsync(path,
            [
                new ResidualUnknown { Id = "b", Question = "B again?", Source = "test" },
                new ResidualUnknown { Id = "c", Question = "C?", Source = "test" },
            ], CancellationToken.None);

            first.Should().Be(2);
            second.Should().Be(1);
            var json = await File.ReadAllTextAsync(path);
            json.Should().Contain("\"open\": 3");
            json.Should().Contain("\"question\": \"B?\"").And.NotContain("B again?");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void JniDiagram_renders_classes_libraries_and_edges()
    {
        var mmd = NativeCommand.BuildJniDiagram("demo",
        [
            ("libbridge.so", new JniBinding { Symbol = "s1", JavaClass = "com.app.Radio", JavaMethod = "setFrequency" }),
            ("libbridge.so", new JniBinding { Symbol = "s2", JavaClass = "com.app.Radio", JavaMethod = "getStatus" }),
        ]);

        mmd.Should().Contain("graph LR");
        mmd.Should().Contain("J0[\"com.app.Radio\"]");
        mmd.Should().Contain("N0[(\"libbridge.so\")]");
        mmd.Should().Contain("J0 -->|setFrequency| N0").And.Contain("J0 -->|getStatus| N0");
    }

    private sealed class FakeProvider : Iaet.Core.Abstractions.IToolProvider
    {
        public string Name => "fake-engine";
        public IReadOnlyList<string> Capabilities { get; } = ["nothing"];
        public Task<ToolHealth> CheckAsync(CancellationToken ct = default) =>
            Task.FromResult(new ToolHealth { Name = Name, Capabilities = Capabilities, Available = false, Message = "install it" });
    }
}
