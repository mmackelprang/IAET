using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public class CSharpRecordGeneratorTests
{
    [Fact]
    public void Generate_BasicObject_ProducesRecord()
    {
        var map = JsonTypeMap.Analyze("""{"name":"Alice","age":30,"active":true}""");

        var code = CSharpRecordGenerator.Generate(map, "MyResponse");

        code.Should().Contain("public sealed record MyResponse");
        code.Should().Contain("string Name");
        code.Should().Contain("double Age");
        code.Should().Contain("bool Active");
    }

    [Fact]
    public void Generate_NullableField_AppendsQuestionMark()
    {
        var map = TypeMerger.Merge(new[]
        {
            JsonTypeMap.Analyze("""{"email":"a@b.com"}"""),
            JsonTypeMap.Analyze("""{"email":null}"""),
        }).MergedMap;

        var code = CSharpRecordGenerator.Generate(map, "MyResponse");

        code.Should().Contain("string? Email");
    }

    [Fact]
    public void Generate_NestedObjectAndPascalCase_ProducesNestedRecord()
    {
        var map = JsonTypeMap.Analyze("""{"user_name":"Alice","address":{"zip_code":"12345"}}""");

        var code = CSharpRecordGenerator.Generate(map, "MyResponse");

        code.Should().Contain("string UserName");
        code.Should().Contain("AddressType Address");
        code.Should().Contain("public sealed record AddressType");
        code.Should().Contain("string ZipCode");
    }
}
