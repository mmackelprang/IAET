using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public class OpenApiGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidYamlStructure()
    {
        var ctx  = TestContextFactory.MakeContext();
        var yaml = OpenApiGenerator.Generate(ctx);

        yaml.Should().Contain("openapi: '3.1.0'");
        yaml.Should().Contain("info:");
        yaml.Should().Contain("TestApp");
        yaml.Should().Contain("servers:");
    }

    [Fact]
    public void Generate_ContainsPaths()
    {
        var ctx  = TestContextFactory.MakeContext();
        var yaml = OpenApiGenerator.Generate(ctx);

        yaml.Should().Contain("paths:");
        yaml.Should().Contain("/api/users/{id}");
        yaml.Should().Contain("get:");
    }

    [Fact]
    public void Generate_ContainsSchemaComponents()
    {
        var ctx  = TestContextFactory.MakeContext();
        var yaml = OpenApiGenerator.Generate(ctx);

        yaml.Should().Contain("components:");
        yaml.Should().Contain("schemas:");
        // The schema fragment from the test context
        yaml.Should().Contain("type: object");
    }
}
