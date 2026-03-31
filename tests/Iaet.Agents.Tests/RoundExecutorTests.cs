using FluentAssertions;
using Iaet.Agents;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using NSubstitute;

namespace Iaet.Agents.Tests;

public sealed class RoundExecutorTests
{
    [Fact]
    public async Task Execute_dispatches_to_matching_agents_in_parallel()
    {
        var agentA = Substitute.For<IInvestigationAgent>();
        agentA.AgentName.Returns("agent-a");
        agentA.ExecuteAsync(Arg.Any<AgentDispatch>(), Arg.Any<ProjectConfig>(), Arg.Any<CancellationToken>())
            .Returns(new AgentFindings { Agent = "agent-a", RoundNumber = 1 });

        var agentB = Substitute.For<IInvestigationAgent>();
        agentB.AgentName.Returns("agent-b");
        agentB.ExecuteAsync(Arg.Any<AgentDispatch>(), Arg.Any<ProjectConfig>(), Arg.Any<CancellationToken>())
            .Returns(new AgentFindings { Agent = "agent-b", RoundNumber = 1 });

        var executor = new RoundExecutor([agentA, agentB]);
        var plan = new RoundPlan
        {
            RoundNumber = 1, Rationale = "test",
            Dispatches = [
                new AgentDispatch { Agent = "agent-a", Targets = ["url1"] },
                new AgentDispatch { Agent = "agent-b", Targets = ["url2"] },
            ],
        };
        var results = await executor.ExecuteRoundAsync(plan, MakeConfig());
        results.Should().HaveCount(2);
        results.Select(f => f.Agent).Should().Contain(["agent-a", "agent-b"]);
    }

    [Fact]
    public async Task Execute_skips_dispatches_with_no_matching_agent()
    {
        var agentA = Substitute.For<IInvestigationAgent>();
        agentA.AgentName.Returns("agent-a");
        agentA.ExecuteAsync(Arg.Any<AgentDispatch>(), Arg.Any<ProjectConfig>(), Arg.Any<CancellationToken>())
            .Returns(new AgentFindings { Agent = "agent-a", RoundNumber = 1 });

        var executor = new RoundExecutor([agentA]);
        var plan = new RoundPlan
        {
            RoundNumber = 1, Rationale = "test",
            Dispatches = [
                new AgentDispatch { Agent = "agent-a" },
                new AgentDispatch { Agent = "nonexistent" },
            ],
        };
        var results = await executor.ExecuteRoundAsync(plan, MakeConfig());
        results.Should().HaveCount(1);
        results[0].Agent.Should().Be("agent-a");
    }

    [Fact]
    public async Task Execute_returns_empty_for_no_dispatches()
    {
        var executor = new RoundExecutor([]);
        var plan = new RoundPlan { RoundNumber = 1, Rationale = "empty" };
        var results = await executor.ExecuteRoundAsync(plan, MakeConfig());
        results.Should().BeEmpty();
    }

    private static ProjectConfig MakeConfig() => new()
    {
        Name = "test", DisplayName = "Test", TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
