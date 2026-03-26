using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public class MarkdownReportGeneratorTests
{
    [Fact]
    public void Generate_ContainsSessionHeader()
    {
        var ctx = TestContextFactory.MakeContext();
        var md  = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("# API Investigation Report");
        md.Should().Contain("TestApp");
        md.Should().Contain("2025-06-01");
    }

    [Fact]
    public void Generate_ListsEndpoints()
    {
        var ctx = TestContextFactory.MakeContext();
        var md  = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("## Endpoint Catalog");
        md.Should().Contain("GET");
        md.Should().Contain("/api/users/{id}");
    }

    [Fact]
    public void Generate_IncludesExampleJson()
    {
        var ctx = TestContextFactory.MakeContext();
        var md  = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("## Endpoint Details");
        md.Should().Contain("Alice");
    }

    [Fact]
    public void Generate_IncludesSchema()
    {
        var ctx = TestContextFactory.MakeContext();
        var md  = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("UsersResponse");
    }

    [Fact]
    public void Generate_IncludesStreams()
    {
        var ctx = TestContextFactory.MakeContextWithStreams();
        var md  = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("## Data Streams");
        md.Should().Contain("WebSocket");
        md.Should().Contain("wss://testapp.example.com/ws/events");
    }

    [Fact]
    public void Generate_RedactsCredentials()
    {
        var ctx = TestContextFactory.MakeContext();
        var md  = MarkdownReportGenerator.Generate(ctx);

        md.Should().Contain("<REDACTED>");
        // Raw Bearer / session tokens must not appear
        md.Should().NotMatchRegex(@"Bearer [A-Za-z0-9]");
    }
}
