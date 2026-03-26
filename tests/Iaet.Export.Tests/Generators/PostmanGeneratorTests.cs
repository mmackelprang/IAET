using System.Text.Json;
using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public class PostmanGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidJson()
    {
        var ctx  = TestContextFactory.MakeContext();
        var json = PostmanGenerator.Generate(ctx);

        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }

    [Fact]
    public void Generate_ContainsItems()
    {
        var ctx  = TestContextFactory.MakeContext();
        var json = PostmanGenerator.Generate(ctx);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("item", out var items).Should().BeTrue();
        items.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_RedactedHeadersPresent()
    {
        var ctx  = TestContextFactory.MakeContext();
        var json = PostmanGenerator.Generate(ctx);

        json.Should().Contain("<REDACTED>");
    }
}
