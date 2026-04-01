using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Projects;

namespace Iaet.Projects.Tests;

public sealed class ProjectStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly ProjectStore _store;

    public ProjectStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _store = new ProjectStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Create_creates_directory_and_project_json()
    {
        var config = MakeConfig("test-project");
        var result = await _store.CreateAsync(config);
        result.Name.Should().Be("test-project");
        var dir = _store.GetProjectDirectory("test-project");
        Directory.Exists(dir).Should().BeTrue();
        File.Exists(Path.Combine(dir, "project.json")).Should().BeTrue();
    }

    [Fact]
    public async Task Create_creates_subdirectories()
    {
        await _store.CreateAsync(MakeConfig("test-project"));
        var dir = _store.GetProjectDirectory("test-project");
        Directory.Exists(Path.Combine(dir, "rounds")).Should().BeTrue();
        Directory.Exists(Path.Combine(dir, "output")).Should().BeTrue();
        Directory.Exists(Path.Combine(dir, "output", "diagrams")).Should().BeTrue();
        Directory.Exists(Path.Combine(dir, "knowledge")).Should().BeTrue();
    }

    [Fact]
    public async Task Load_returns_null_for_nonexistent()
    {
        var result = await _store.LoadAsync("nope");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Load_round_trips_config()
    {
        var config = MakeConfig("roundtrip");
        await _store.CreateAsync(config);
        var loaded = await _store.LoadAsync("roundtrip");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("roundtrip");
        loaded.TargetType.Should().Be(TargetType.Web);
        loaded.EntryPoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_returns_all_projects()
    {
        await _store.CreateAsync(MakeConfig("alpha"));
        await _store.CreateAsync(MakeConfig("beta"));
        var list = await _store.ListAsync();
        list.Should().HaveCount(2);
        list.Select(p => p.Name).Should().Contain(["alpha", "beta"]);
    }

    [Fact]
    public async Task Save_updates_existing_config()
    {
        var config = MakeConfig("updatable");
        await _store.CreateAsync(config);
        var updated = config with { Status = ProjectStatus.Investigating, CurrentRound = 2 };
        await _store.SaveAsync(updated);
        var loaded = await _store.LoadAsync("updatable");
        loaded!.Status.Should().Be(ProjectStatus.Investigating);
        loaded.CurrentRound.Should().Be(2);
    }

    [Fact]
    public async Task Archive_sets_status_to_archived()
    {
        await _store.CreateAsync(MakeConfig("archivable"));
        await _store.ArchiveAsync("archivable");
        var loaded = await _store.LoadAsync("archivable");
        loaded!.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public async Task RefreshStatus_new_project_with_no_content_stays_new()
    {
        await _store.CreateAsync(MakeConfig("empty-project"));
        var result = await _store.RefreshStatusAsync("empty-project");
        result.Status.Should().Be(ProjectStatus.New);
    }

    [Fact]
    public async Task RefreshStatus_project_with_knowledge_files_becomes_investigating()
    {
        await _store.CreateAsync(MakeConfig("active-project"));
        var knowledgeDir = Path.Combine(_store.GetProjectDirectory("active-project"), "knowledge");
        Directory.CreateDirectory(knowledgeDir);
        await File.WriteAllTextAsync(Path.Combine(knowledgeDir, "endpoints.json"), "{}");

        var result = await _store.RefreshStatusAsync("active-project");
        result.Status.Should().Be(ProjectStatus.Investigating);
    }

    [Fact]
    public async Task RefreshStatus_archived_project_stays_archived_even_with_content()
    {
        await _store.CreateAsync(MakeConfig("archived-project"));
        await _store.ArchiveAsync("archived-project");

        // Add content that would normally trigger Investigating
        var knowledgeDir = Path.Combine(_store.GetProjectDirectory("archived-project"), "knowledge");
        Directory.CreateDirectory(knowledgeDir);
        await File.WriteAllTextAsync(Path.Combine(knowledgeDir, "endpoints.json"), "{}");

        var result = await _store.RefreshStatusAsync("archived-project");
        result.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public async Task RefreshStatus_project_with_captures_becomes_investigating()
    {
        await _store.CreateAsync(MakeConfig("capture-project"));
        var capturesDir = Path.Combine(_store.GetProjectDirectory("capture-project"), "captures");
        Directory.CreateDirectory(capturesDir);
        await File.WriteAllTextAsync(Path.Combine(capturesDir, "test.iaet.json.gz"), "dummy");

        var result = await _store.RefreshStatusAsync("capture-project");
        result.Status.Should().Be(ProjectStatus.Investigating);
    }

    [Fact]
    public async Task RefreshStatus_complete_project_stays_complete()
    {
        var config = MakeConfig("complete-project");
        await _store.CreateAsync(config);
        var updated = config with { Status = ProjectStatus.Complete };
        await _store.SaveAsync(updated);

        var knowledgeDir = Path.Combine(_store.GetProjectDirectory("complete-project"), "knowledge");
        await File.WriteAllTextAsync(Path.Combine(knowledgeDir, "endpoints.json"), "{}");

        var result = await _store.RefreshStatusAsync("complete-project");
        result.Status.Should().Be(ProjectStatus.Complete);
    }

    private static ProjectConfig MakeConfig(string name) => new()
    {
        Name = name,
        DisplayName = $"Test {name}",
        TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
