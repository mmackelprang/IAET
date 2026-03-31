using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class ProjectConfigTests
{
    [Fact]
    public void Can_create_with_required_fields()
    {
        var config = new ProjectConfig
        {
            Name = "google-voice",
            DisplayName = "Google Voice Investigation",
            TargetType = TargetType.Web,
            EntryPoints = [new EntryPoint { Url = "https://voice.google.com", Label = "Main app" }],
        };

        config.Name.Should().Be("google-voice");
        config.Status.Should().Be(ProjectStatus.New);
        config.CurrentRound.Should().Be(0);
        config.AuthRequired.Should().BeFalse();
    }

    [Fact]
    public void Can_create_with_auth_and_focus_areas()
    {
        var config = new ProjectConfig
        {
            Name = "gv",
            DisplayName = "GV",
            TargetType = TargetType.Web,
            EntryPoints = [new EntryPoint { Url = "https://voice.google.com", Label = "Main" }],
            AuthRequired = true,
            AuthMethod = "browser-login",
            FocusAreas = ["call-signaling", "sms-api"],
        };

        config.AuthRequired.Should().BeTrue();
        config.AuthMethod.Should().Be("browser-login");
        config.FocusAreas.Should().HaveCount(2);
    }

    [Fact]
    public void RoundPlan_holds_dispatches_and_human_actions()
    {
        var plan = new RoundPlan
        {
            RoundNumber = 2,
            Rationale = "JS bundle found unobserved endpoints",
            Dispatches =
            [
                new AgentDispatch
                {
                    Agent = "js-bundle-analyzer",
                    Targets = ["https://voice.google.com/main-bundle.js"],
                },
            ],
            HumanActions =
            [
                new HumanActionRequest
                {
                    Action = "Place a phone call",
                    Reason = "Need call setup signaling",
                },
            ],
        };

        plan.Dispatches.Should().HaveCount(1);
        plan.HumanActions.Should().HaveCount(1);
    }

    [Fact]
    public void AgentFindings_carries_confidence_levels()
    {
        var findings = new AgentFindings
        {
            Agent = "network-capture",
            RoundNumber = 1,
            Endpoints =
            [
                new DiscoveredEndpoint
                {
                    Signature = "GET /api/v1/users",
                    Confidence = ConfidenceLevel.High,
                    ObservationCount = 5,
                    Sources = ["network-capture-round-1"],
                },
            ],
        };

        findings.Endpoints.Should().HaveCount(1);
        findings.Endpoints[0].Confidence.Should().Be(ConfidenceLevel.High);
    }
}
