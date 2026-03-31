using System.Text.Json;
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Projects;

namespace Iaet.Projects.Tests;

public sealed class KnowledgeStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly ProjectStore _projectStore;
    private readonly KnowledgeStore _store;

    public KnowledgeStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _projectStore = new ProjectStore(_rootDir);
        _store = new KnowledgeStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Read_returns_null_for_nonexistent()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        var result = await _store.ReadAsync("proj", "endpoints.json");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Write_and_Read_round_trip()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        using var doc = JsonDocument.Parse("""{"endpoints": [{"name": "GET /api"}]}""");
        await _store.WriteAsync("proj", "endpoints.json", doc);
        using var loaded = await _store.ReadAsync("proj", "endpoints.json");
        loaded.Should().NotBeNull();
        loaded!.RootElement.GetProperty("endpoints").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ListFiles_returns_written_files()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        using var doc = JsonDocument.Parse("{}");
        await _store.WriteAsync("proj", "endpoints.json", doc);
        await _store.WriteAsync("proj", "cookies.json", doc);
        var files = await _store.ListFilesAsync("proj");
        files.Should().Contain(["endpoints.json", "cookies.json"]);
    }

    private static ProjectConfig MakeConfig(string name) => new()
    {
        Name = name,
        DisplayName = name,
        TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
