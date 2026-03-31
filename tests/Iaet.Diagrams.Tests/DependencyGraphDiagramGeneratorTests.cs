using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class DependencyGraphDiagramGeneratorTests
{
    [Fact]
    public void Generate_creates_flowchart_from_dependencies()
    {
        var deps = new List<RequestDependency>
        {
            new() { From = "POST /login", To = "GET /api/data", Reason = "Auth token required" },
            new() { From = "GET /session", To = "GET /api/calls", Reason = "Session ID required" },
        };

        var mermaid = DependencyGraphDiagramGenerator.Generate("Auth Dependencies", deps);

        mermaid.Should().StartWith("flowchart TD");
        mermaid.Should().Contain("POST /login");
        mermaid.Should().Contain("GET /api/data");
        mermaid.Should().Contain("Auth token required");
    }

    [Fact]
    public void Generate_from_auth_chains()
    {
        var chains = new List<AuthChain>
        {
            new()
            {
                Name = "Session flow",
                Steps =
                [
                    new AuthChainStep { Endpoint = "POST /login", Provides = "session_cookie", Type = "cookie" },
                    new AuthChainStep { Endpoint = "GET /api/data", Consumes = "session_cookie", Type = "cookie" },
                ],
            },
        };

        var mermaid = DependencyGraphDiagramGenerator.GenerateFromAuthChains("Auth Chains", chains);

        mermaid.Should().Contain("POST /login");
        mermaid.Should().Contain("session_cookie");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var mermaid = DependencyGraphDiagramGenerator.Generate("Empty", []);
        mermaid.Should().Contain("flowchart TD");
    }
}
