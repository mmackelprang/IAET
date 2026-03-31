using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class ConfidenceAnnotatorTests
{
    [Fact]
    public void Annotate_adds_note_to_mermaid()
    {
        var diagram = "sequenceDiagram\n    Browser->>Server: GET /api";

        var annotated = ConfidenceAnnotator.Annotate(
            diagram, ConfidenceLevel.High, 5, "network-capture");

        annotated.Should().Contain("Confidence: High");
        annotated.Should().Contain("5 observations");
    }

    [Fact]
    public void Annotate_includes_limitations()
    {
        var diagram = "flowchart TD";

        var annotated = ConfidenceAnnotator.Annotate(
            diagram, ConfidenceLevel.Low, 0, "js-bundle-analyzer",
            ["Extracted from string literal only"]);

        annotated.Should().Contain("Confidence: Low");
        annotated.Should().Contain("Extracted from string literal only");
    }

    [Fact]
    public void Annotate_preserves_original_diagram()
    {
        var diagram = "stateDiagram-v2\n    [*] --> init";

        var annotated = ConfidenceAnnotator.Annotate(
            diagram, ConfidenceLevel.Medium, 2, "protocol-analyzer");

        annotated.Should().Contain("[*] --> init");
    }
}
