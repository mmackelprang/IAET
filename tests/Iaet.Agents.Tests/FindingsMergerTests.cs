using FluentAssertions;
using Iaet.Agents;
using Iaet.Core.Models;

namespace Iaet.Agents.Tests;

public sealed class FindingsMergerTests
{
    [Fact]
    public void Merge_combines_endpoints_from_multiple_agents()
    {
        var findingsA = new AgentFindings
        {
            Agent = "agent-a", RoundNumber = 1,
            Endpoints = [new DiscoveredEndpoint { Signature = "GET /api/users", Confidence = ConfidenceLevel.High, ObservationCount = 3, Sources = ["agent-a"] }],
        };
        var findingsB = new AgentFindings
        {
            Agent = "agent-b", RoundNumber = 1,
            Endpoints = [new DiscoveredEndpoint { Signature = "POST /api/login", Confidence = ConfidenceLevel.Medium, ObservationCount = 1, Sources = ["agent-b"] }],
        };
        var merged = FindingsMerger.Merge([findingsA, findingsB]);
        merged.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_deduplicates_same_endpoint_keeping_highest_confidence()
    {
        var findingsA = new AgentFindings
        {
            Agent = "agent-a", RoundNumber = 1,
            Endpoints = [new DiscoveredEndpoint { Signature = "GET /api/users", Confidence = ConfidenceLevel.Low, ObservationCount = 1, Sources = ["agent-a"] }],
        };
        var findingsB = new AgentFindings
        {
            Agent = "agent-b", RoundNumber = 1,
            Endpoints = [new DiscoveredEndpoint { Signature = "GET /api/users", Confidence = ConfidenceLevel.High, ObservationCount = 5, Sources = ["agent-b"] }],
        };
        var merged = FindingsMerger.Merge([findingsA, findingsB]);
        merged.Should().HaveCount(1);
        merged[0].Confidence.Should().Be(ConfidenceLevel.High);
        merged[0].ObservationCount.Should().Be(6);
        merged[0].Sources.Should().Contain(["agent-a", "agent-b"]);
    }

    [Fact]
    public void Merge_handles_empty_input()
    {
        var merged = FindingsMerger.Merge([]);
        merged.Should().BeEmpty();
    }
}
