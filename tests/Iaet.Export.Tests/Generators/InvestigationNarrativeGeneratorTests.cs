using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class InvestigationNarrativeGeneratorTests
{
    [Fact]
    public void Generate_includes_session_header()
    {
        var ctx = TestContextFactory.MakeContext();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("# Investigation Report");
        narrative.Should().Contain("TestApp");
    }

    [Fact]
    public void Generate_includes_endpoint_summary()
    {
        var ctx = TestContextFactory.MakeContext();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("Endpoints Discovered");
        narrative.Should().Contain("GET /api/users/{id}");
    }

    [Fact]
    public void Generate_includes_stream_summary()
    {
        var ctx = TestContextFactory.MakeContextWithStreams();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("Streams");
        narrative.Should().Contain("WebSocket");
    }

    [Fact]
    public void Generate_includes_schema_summary()
    {
        var ctx = TestContextFactory.MakeContext();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("Schema");
    }
}
