using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public class HtmlReportGeneratorTests
{
    [Fact]
    public void Generate_StartsWithDoctype()
    {
        var ctx  = TestContextFactory.MakeContext();
        var html = HtmlReportGenerator.Generate(ctx);

        html.TrimStart().Should().StartWith("<!DOCTYPE html>");
    }

    [Fact]
    public void Generate_ContainsReportContent()
    {
        var ctx  = TestContextFactory.MakeContext();
        var html = HtmlReportGenerator.Generate(ctx);

        // Core content from the Markdown report should appear in the HTML
        html.Should().Contain("API Investigation Report");
        html.Should().Contain("TestApp");
        html.Should().Contain("Endpoint Catalog");
    }
}
