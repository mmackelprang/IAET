using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class DataFlowMapGeneratorTests
{
    [Fact]
    public void Generate_creates_flowchart_from_requests()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/data"),
            MakeRequest("POST", "https://auth.example.com/token"),
            MakeRequest("GET", "https://api.example.com/users"),
        };

        var mermaid = DataFlowMapGenerator.Generate("Service Map", requests);

        mermaid.Should().StartWith("flowchart TD");
        mermaid.Should().Contain("Browser");
        mermaid.Should().Contain("api_example_com");
        mermaid.Should().Contain("auth_example_com");
    }

    [Fact]
    public void Generate_shows_request_counts_on_edges()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/a"),
            MakeRequest("GET", "https://api.example.com/b"),
            MakeRequest("POST", "https://api.example.com/c"),
        };

        var mermaid = DataFlowMapGenerator.Generate("Map", requests);

        mermaid.Should().Contain("3 requests");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var mermaid = DataFlowMapGenerator.Generate("Empty", []);
        mermaid.Should().Contain("flowchart TD");
    }

    private static CapturedRequest MakeRequest(string method, string url) => new()
    {
        Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method, Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200, ResponseHeaders = new Dictionary<string, string>(), DurationMs = 50,
    };
}
