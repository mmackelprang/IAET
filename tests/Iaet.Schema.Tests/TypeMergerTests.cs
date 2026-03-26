using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public class TypeMergerTests
{
    [Fact]
    public void Merge_IdenticalSchemas_ProducesNoWarnings()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"name":"Alice","age":30}"""),
            JsonTypeMap.Analyze("""{"name":"Bob","age":25}"""),
        };

        var result = TypeMerger.Merge(maps);

        result.Warnings.Should().BeEmpty();
        result.MergedMap.Fields.Should().ContainKey("name");
        result.MergedMap.Fields["name"].JsonType.Should().Be(JsonFieldType.String);
        result.MergedMap.Fields.Should().ContainKey("age");
        result.MergedMap.Fields["age"].JsonType.Should().Be(JsonFieldType.Number);
    }

    [Fact]
    public void Merge_NullableField_MarksFieldAsNullable()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"name":"Alice","email":"a@b.com"}"""),
            JsonTypeMap.Analyze("""{"name":"Bob","email":null}"""),
        };

        var result = TypeMerger.Merge(maps);

        result.MergedMap.Fields["email"].IsNullable.Should().BeTrue();
        result.MergedMap.Fields["email"].JsonType.Should().Be(JsonFieldType.String);
    }

    [Fact]
    public void Merge_TypeConflict_ProducesWarning()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"value":"hello"}"""),
            JsonTypeMap.Analyze("""{"value":42}"""),
        };

        var result = TypeMerger.Merge(maps);

        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("value");
    }

    [Fact]
    public void Merge_OptionalField_MarksAsNullable()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"name":"Alice","email":"a@b.com"}"""),
            JsonTypeMap.Analyze("""{"name":"Bob"}"""),
        };

        var result = TypeMerger.Merge(maps);

        result.MergedMap.Fields.Should().ContainKey("email");
        result.MergedMap.Fields["email"].IsNullable.Should().BeTrue();
    }
}
