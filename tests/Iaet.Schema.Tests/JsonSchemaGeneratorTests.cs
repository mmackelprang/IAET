using System.Text.Json;
using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public class JsonSchemaGeneratorTests
{
    [Fact]
    public void Generate_BasicObject_ProducesValidJsonSchema()
    {
        var map = JsonTypeMap.Analyze("""{"name":"Alice","age":30,"active":true}""");

        var schema = JsonSchemaGenerator.Generate(map);

        using var doc = JsonDocument.Parse(schema);
        var root = doc.RootElement;
        root.GetProperty("$schema").GetString().Should().Be("http://json-schema.org/draft-07/schema#");
        root.GetProperty("type").GetString().Should().Be("object");

        var props = root.GetProperty("properties");
        props.GetProperty("name").GetProperty("type").GetString().Should().Be("string");
        props.GetProperty("age").GetProperty("type").GetString().Should().Be("number");
        props.GetProperty("active").GetProperty("type").GetString().Should().Be("boolean");
    }

    [Fact]
    public void Generate_NullableField_UsesTypeArray()
    {
        var map = TypeMerger.Merge(new[]
        {
            JsonTypeMap.Analyze("""{"email":"a@b.com"}"""),
            JsonTypeMap.Analyze("""{"email":null}"""),
        }).MergedMap;

        var schema = JsonSchemaGenerator.Generate(map);

        using var doc = JsonDocument.Parse(schema);
        var emailType = doc.RootElement.GetProperty("properties").GetProperty("email").GetProperty("type");
        emailType.ValueKind.Should().Be(JsonValueKind.Array);
        var types = emailType.EnumerateArray().Select(e => e.GetString()).ToList();
        types.Should().Contain("string");
        types.Should().Contain("null");
    }

    [Fact]
    public void Generate_NestedObjectAndArray_ProducesNestedSchema()
    {
        var map = JsonTypeMap.Analyze("""{"user":{"name":"Alice"},"tags":["a","b"]}""");

        var schema = JsonSchemaGenerator.Generate(map);

        using var doc = JsonDocument.Parse(schema);
        var props = doc.RootElement.GetProperty("properties");

        // Nested object
        var user = props.GetProperty("user");
        user.GetProperty("type").GetString().Should().Be("object");
        user.GetProperty("properties").GetProperty("name").GetProperty("type").GetString().Should().Be("string");

        // Array
        var tags = props.GetProperty("tags");
        tags.GetProperty("type").GetString().Should().Be("array");
        tags.GetProperty("items").GetProperty("type").GetString().Should().Be("string");
    }
}
