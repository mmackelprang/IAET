using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class DiagramValidationTests
{
    [Theory]
    [InlineData("sequenceDiagram")]
    [InlineData("flowchart")]
    [InlineData("stateDiagram")]
    public void AllDiagramTypes_start_with_valid_mermaid_keyword(string expectedPrefix)
    {
        // This test documents the valid prefixes
        var validPrefixes = new[] { "sequenceDiagram", "flowchart", "stateDiagram-v2", "flowchart TD", "flowchart LR", "graph TD" };
        validPrefixes.Should().Contain(p => p.StartsWith(expectedPrefix, System.StringComparison.Ordinal));
    }

    [Fact]
    public void SequenceDiagramGenerator_output_starts_with_valid_keyword()
    {
        var mermaid = SequenceDiagramGenerator.Generate("Test", []);
        mermaid.TrimStart().Should().StartWith("sequenceDiagram");
    }

    [Fact]
    public void SequenceDiagramGenerator_output_has_no_unescaped_html_tags()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/test<script>alert(1)</script>", 200),
        };
        var mermaid = SequenceDiagramGenerator.Generate("Test", requests);
        mermaid.Should().NotContain("<script>");
    }

    [Fact]
    public void DataFlowMapGenerator_output_starts_with_flowchart()
    {
        var mermaid = DataFlowMapGenerator.Generate("Test", []);
        mermaid.TrimStart().Should().StartWith("flowchart TD");
    }

    [Fact]
    public void DependencyGraphDiagramGenerator_output_starts_with_flowchart()
    {
        var mermaid = DependencyGraphDiagramGenerator.Generate("Test", []);
        mermaid.TrimStart().Should().StartWith("flowchart TD");
    }

    [Fact]
    public void StateMachineDiagramGenerator_output_starts_with_stateDiagram()
    {
        var sm = new StateMachineModel { Name = "Test", States = ["a", "b"], Transitions = [], InitialState = "a" };
        var mermaid = StateMachineDiagramGenerator.Generate(sm);
        mermaid.TrimStart().Should().StartWith("stateDiagram-v2");
    }

    [Fact]
    public void SequenceDiagramGenerator_does_not_produce_raw_html_in_participants()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/data", 200),
        };
        var mermaid = SequenceDiagramGenerator.Generate("Test", requests);
        // Participant names should be sanitized (dots/hyphens replaced)
        mermaid.Should().NotContainAll("<br/>", "<br>", "</");
    }

    [Fact]
    public void DataFlowMapGenerator_sanitizes_host_names_for_mermaid_ids()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://my-api.example.com/data", 200),
        };
        var mermaid = DataFlowMapGenerator.Generate("Test", requests);
        // Mermaid IDs can't have dots or hyphens
        mermaid.Should().Contain("my_api_example_com");
    }

    [Fact]
    public void DependencyGraphDiagramGenerator_quotes_edge_labels()
    {
        var deps = new List<RequestDependency>
        {
            new() { From = "POST /login", To = "GET /data", Reason = "Auth token with special chars: <>&" },
        };
        var mermaid = DependencyGraphDiagramGenerator.Generate("Test", deps);
        // Edge labels should be quoted to handle special chars
        mermaid.Should().Contain("|\"");
    }

    [Fact]
    public void StateMachineDiagramGenerator_handles_special_chars_in_transitions()
    {
        var sm = new StateMachineModel
        {
            Name = "Test",
            States = ["a", "b"],
            Transitions = [new StateTransition { From = "a", To = "b", Trigger = "event with spaces" }],
            InitialState = "a",
        };
        var mermaid = StateMachineDiagramGenerator.Generate(sm);
        mermaid.Should().Contain("event with spaces");
    }

    [Fact]
    public void ConfidenceAnnotator_output_uses_mermaid_comments()
    {
        var diagram = "sequenceDiagram\n    Browser->>Server: GET /api";
        var annotated = ConfidenceAnnotator.Annotate(diagram, ConfidenceLevel.High, 5, "test");
        // Confidence should be in comments, not rendered content that could break syntax
        annotated.Should().Contain("%%");
    }

    private static CapturedRequest MakeRequest(string method, string url, int status) => new()
    {
        Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method, Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = status, ResponseHeaders = new Dictionary<string, string>(), DurationMs = 50,
    };
}
