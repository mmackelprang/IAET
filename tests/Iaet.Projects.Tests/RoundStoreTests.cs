using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Projects;

namespace Iaet.Projects.Tests;

public sealed class RoundStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly ProjectStore _projectStore;
    private readonly RoundStore _store;

    public RoundStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _projectStore = new ProjectStore(_rootDir);
        _store = new RoundStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task CreateRound_creates_numbered_directory_and_plan()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        var plan = new RoundPlan
        {
            RoundNumber = 1,
            Rationale = "Initial capture",
            Dispatches = [new AgentDispatch { Agent = "network-capture", Targets = ["https://example.com"] }],
        };
        var roundNum = await _store.CreateRoundAsync("proj", plan);
        roundNum.Should().Be(1);
        var roundDir = Path.Combine(_rootDir, "proj", "rounds", "001-round");
        Directory.Exists(roundDir).Should().BeTrue();
        File.Exists(Path.Combine(roundDir, "plan.json")).Should().BeTrue();
    }

    [Fact]
    public async Task GetPlan_round_trips()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        var plan = new RoundPlan { RoundNumber = 1, Rationale = "Test rationale" };
        await _store.CreateRoundAsync("proj", plan);
        var loaded = await _store.GetPlanAsync("proj", 1);
        loaded.Should().NotBeNull();
        loaded!.Rationale.Should().Be("Test rationale");
    }

    [Fact]
    public async Task SaveFindings_and_GetFindings_round_trip()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        await _store.CreateRoundAsync("proj", new RoundPlan { RoundNumber = 1, Rationale = "test" });
        var findings = new AgentFindings
        {
            Agent = "network-capture",
            RoundNumber = 1,
            Endpoints = [new DiscoveredEndpoint { Signature = "GET /api", Confidence = ConfidenceLevel.High, ObservationCount = 3 }],
        };
        await _store.SaveFindingsAsync("proj", 1, findings);
        var loaded = await _store.GetFindingsAsync("proj", 1);
        loaded.Should().HaveCount(1);
        loaded[0].Agent.Should().Be("network-capture");
        loaded[0].Endpoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveFindings_accumulates_multiple_agents()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        await _store.CreateRoundAsync("proj", new RoundPlan { RoundNumber = 1, Rationale = "test" });
        await _store.SaveFindingsAsync("proj", 1, new AgentFindings { Agent = "agent-a", RoundNumber = 1 });
        await _store.SaveFindingsAsync("proj", 1, new AgentFindings { Agent = "agent-b", RoundNumber = 1 });
        var loaded = await _store.GetFindingsAsync("proj", 1);
        loaded.Should().HaveCount(2);
    }

    private static ProjectConfig MakeConfig(string name) => new()
    {
        Name = name,
        DisplayName = name,
        TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
