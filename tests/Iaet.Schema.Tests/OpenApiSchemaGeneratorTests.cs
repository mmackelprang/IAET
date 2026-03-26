using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public class OpenApiSchemaGeneratorTests
{
    [Fact]
    public void Generate_BasicObject_ProducesYaml()
    {
        var map = JsonTypeMap.Analyze("""{"name":"Alice","age":30}""");

        var yaml = OpenApiSchemaGenerator.Generate(map);

        yaml.Should().Contain("type: object");
        yaml.Should().Contain("properties:");
        yaml.Should().Contain("name:");
        yaml.Should().Contain("type: string");
        yaml.Should().Contain("age:");
        yaml.Should().Contain("type: number");
    }

    [Fact]
    public void Generate_NullableField_MarksNullable()
    {
        var map = TypeMerger.Merge(new[]
        {
            JsonTypeMap.Analyze("""{"email":"a@b.com"}"""),
            JsonTypeMap.Analyze("""{"email":null}"""),
        }).MergedMap;

        var yaml = OpenApiSchemaGenerator.Generate(map);

        yaml.Should().Contain("nullable: true");
    }

    [Fact]
    public void Generate_ArrayField_ProducesItems()
    {
        var map = JsonTypeMap.Analyze("""{"tags":["a","b"]}""");

        var yaml = OpenApiSchemaGenerator.Generate(map);

        yaml.Should().Contain("tags:");
        yaml.Should().Contain("type: array");
        yaml.Should().Contain("items:");
        yaml.Should().Contain("type: string");
    }
}
