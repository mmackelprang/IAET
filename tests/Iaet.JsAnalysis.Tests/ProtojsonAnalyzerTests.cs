using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class ProtojsonAnalyzerTests
{
    [Fact]
    public void IsProtojson_returns_true_for_root_array()
    {
        ProtojsonAnalyzer.IsProtojson("[null, \"hello\", 42]").Should().BeTrue();
    }

    [Fact]
    public void IsProtojson_returns_false_for_object()
    {
        ProtojsonAnalyzer.IsProtojson("{\"key\": \"value\"}").Should().BeFalse();
    }

    [Fact]
    public void IsProtojson_returns_false_for_empty()
    {
        ProtojsonAnalyzer.IsProtojson("").Should().BeFalse();
    }

    [Fact]
    public void Analyze_infers_field_types_by_position()
    {
        var schema = ProtojsonAnalyzer.Analyze("[null, \"Mark\", 42, true, [1,2,3]]");

        schema.Fields.Should().HaveCount(5);
        schema.Fields[0].InferredType.Should().Be("null");
        schema.Fields[1].InferredType.Should().Be("string");
        schema.Fields[2].InferredType.Should().Be("integer");
        schema.Fields[3].InferredType.Should().Be("boolean");
        schema.Fields[4].InferredType.Should().Be("array");
    }

    [Fact]
    public void Analyze_detects_nested_arrays()
    {
        var schema = ProtojsonAnalyzer.Analyze("[\"name\", [\"nested1\", 123]]");

        schema.Fields[1].NestedArray.Should().NotBeNull();
        schema.Fields[1].NestedArray!.Fields.Should().HaveCount(2);
        schema.Fields[1].NestedArray!.Fields[0].InferredType.Should().Be("string");
        schema.Fields[1].NestedArray!.Fields[1].InferredType.Should().Be("integer");
    }

    [Fact]
    public void Merge_combines_multiple_samples()
    {
        var schema1 = ProtojsonAnalyzer.Analyze("[null, \"a\", 1]");
        var schema2 = ProtojsonAnalyzer.Analyze("[\"id-123\", \"b\", 2]");

        var merged = ProtojsonAnalyzer.Merge([schema1, schema2]);

        merged.Fields.Should().HaveCount(3);
        merged.Fields[0].InferredType.Should().Be("string"); // null in one, string in other
        merged.Fields[0].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Merge_handles_different_lengths()
    {
        var short_ = ProtojsonAnalyzer.Analyze("[\"a\"]");
        var long_ = ProtojsonAnalyzer.Analyze("[\"b\", 42, true]");

        var merged = ProtojsonAnalyzer.Merge([short_, long_]);

        merged.Fields.Should().HaveCount(3);
        merged.Fields[1].IsNullable.Should().BeTrue(); // missing in short sample
    }

    [Fact]
    public void Describe_produces_readable_output()
    {
        var schema = ProtojsonAnalyzer.Analyze("[null, \"hello\", 42]");
        var desc = ProtojsonAnalyzer.Describe(schema);

        desc.Should().Contain("[0] null");
        desc.Should().Contain("[1] string");
        desc.Should().Contain("[2] integer");
    }
}
