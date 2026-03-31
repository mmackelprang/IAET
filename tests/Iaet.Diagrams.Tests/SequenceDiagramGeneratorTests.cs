using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class SequenceDiagramGeneratorTests
{
    [Fact]
    public void Generate_produces_mermaid_sequence_diagram()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/session", 200, t: 1),
            MakeRequest("GET", "https://api.example.com/users/123", 200, t: 2),
            MakeRequest("POST", "https://api.example.com/messages", 201, t: 3),
        };

        var mermaid = SequenceDiagramGenerator.Generate("API Flow", requests);

        mermaid.Should().StartWith("sequenceDiagram");
        mermaid.Should().Contain("Browser");
        mermaid.Should().Contain("api_example_com");
        mermaid.Should().Contain("GET /session");
        mermaid.Should().Contain("200");
    }

    [Fact]
    public void Generate_groups_by_host()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/data", 200, t: 1),
            MakeRequest("GET", "https://auth.example.com/token", 200, t: 2),
        };

        var mermaid = SequenceDiagramGenerator.Generate("Multi-host", requests);

        mermaid.Should().Contain("api_example_com");
        mermaid.Should().Contain("auth_example_com");
    }

    [Fact]
    public void Generate_handles_error_responses()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/secret", 403, t: 1),
        };

        var mermaid = SequenceDiagramGenerator.Generate("Errors", requests);

        mermaid.Should().Contain("403");
    }

    [Fact]
    public void Generate_handles_empty_requests()
    {
        var mermaid = SequenceDiagramGenerator.Generate("Empty", []);

        mermaid.Should().Contain("sequenceDiagram");
        mermaid.Should().Contain("Note");
    }

    private static CapturedRequest MakeRequest(string method, string url, int status, int t) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow.AddSeconds(t),
        HttpMethod = method,
        Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = status,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 50,
    };
}
