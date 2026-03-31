using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class AnalysisModelTests
{
    [Fact]
    public void ExtractedUrl_holds_url_with_source_context()
    {
        var url = new ExtractedUrl
        {
            Url = "/api/v1/users",
            HttpMethod = "GET",
            SourceFile = "main-bundle.js",
            LineNumber = 4521,
            Confidence = ConfidenceLevel.High,
            Context = "fetch(\"/api/v1/users\")",
        };

        url.Url.Should().Be("/api/v1/users");
        url.SourceFile.Should().Be("main-bundle.js");
    }

    [Fact]
    public void RequestDependency_describes_ordering_constraint()
    {
        var dep = new RequestDependency
        {
            From = "GET /api/session",
            To = "GET /api/calls",
            Reason = "X-Session-Id header required",
            SharedField = "sessionId",
        };

        dep.From.Should().Be("GET /api/session");
        dep.To.Should().Be("GET /api/calls");
    }

    [Fact]
    public void AuthChain_traces_credential_flow()
    {
        var chain = new AuthChain
        {
            Name = "Google Voice session",
            Steps =
            [
                new AuthChainStep { Endpoint = "POST /login", Provides = "session_cookie", Type = "cookie" },
                new AuthChainStep { Endpoint = "GET /api/token", Provides = "access_token", Type = "header" },
                new AuthChainStep { Endpoint = "GET /api/calls", Consumes = "access_token", Type = "header" },
            ],
        };

        chain.Steps.Should().HaveCount(3);
    }

    [Fact]
    public void StreamAnalysisResult_carries_protocol_findings()
    {
        var result = new StreamAnalysisResult
        {
            StreamId = Guid.NewGuid(),
            Protocol = StreamProtocol.WebSocket,
            MessageTypes = ["control", "data", "heartbeat"],
            SubProtocol = "graphql-ws",
            Confidence = ConfidenceLevel.High,
        };

        result.MessageTypes.Should().HaveCount(3);
        result.SubProtocol.Should().Be("graphql-ws");
    }

    [Fact]
    public void StateMachineModel_has_states_and_transitions()
    {
        var sm = new StateMachineModel
        {
            Name = "WebRTC Connection",
            States = ["new", "connecting", "connected", "disconnected"],
            Transitions =
            [
                new StateTransition { From = "new", To = "connecting", Trigger = "createOffer" },
                new StateTransition { From = "connecting", To = "connected", Trigger = "iceComplete" },
            ],
            InitialState = "new",
        };

        sm.States.Should().HaveCount(4);
        sm.Transitions.Should().HaveCount(2);
    }
}
