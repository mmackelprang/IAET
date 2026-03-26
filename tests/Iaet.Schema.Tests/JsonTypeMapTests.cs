using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public class JsonTypeMapTests
{
    [Fact]
    public void Analyze_SimpleObjectFields_ReturnsCorrectFieldTypes()
    {
        var json = """{"name":"Alice","age":30,"active":true}""";

        var map = JsonTypeMap.Analyze(json);

        map.Fields.Should().ContainKey("name");
        map.Fields["name"].JsonType.Should().Be(JsonFieldType.String);
        map.Fields.Should().ContainKey("age");
        map.Fields["age"].JsonType.Should().Be(JsonFieldType.Number);
        map.Fields.Should().ContainKey("active");
        map.Fields["active"].JsonType.Should().Be(JsonFieldType.Boolean);
    }

    [Fact]
    public void Analyze_NestedObject_ReturnsNestedFields()
    {
        var json = """{"user":{"name":"Alice","email":"a@b.com"}}""";

        var map = JsonTypeMap.Analyze(json);

        map.Fields.Should().ContainKey("user");
        map.Fields["user"].JsonType.Should().Be(JsonFieldType.Object);
        map.Fields["user"].NestedFields.Should().NotBeNull();
        map.Fields["user"].NestedFields!.Should().ContainKey("name");
        map.Fields["user"].NestedFields!["name"].JsonType.Should().Be(JsonFieldType.String);
        map.Fields["user"].NestedFields!.Should().ContainKey("email");
        map.Fields["user"].NestedFields!["email"].JsonType.Should().Be(JsonFieldType.String);
    }

    [Fact]
    public void Analyze_ArrayWithItemType_ReturnsArrayFieldWithItemType()
    {
        var json = """{"tags":["a","b","c"]}""";

        var map = JsonTypeMap.Analyze(json);

        map.Fields.Should().ContainKey("tags");
        map.Fields["tags"].JsonType.Should().Be(JsonFieldType.Array);
        map.Fields["tags"].ArrayItemType.Should().NotBeNull();
        map.Fields["tags"].ArrayItemType!.JsonType.Should().Be(JsonFieldType.String);
    }

    [Fact]
    public void Analyze_NullField_ReturnsNullTypeAndIsNullable()
    {
        var json = """{"name":"Alice","address":null}""";

        var map = JsonTypeMap.Analyze(json);

        map.Fields.Should().ContainKey("address");
        map.Fields["address"].JsonType.Should().Be(JsonFieldType.Null);
        map.Fields["address"].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Analyze_EmptyObject_ReturnsEmptyFields()
    {
        var json = """{}""";

        var map = JsonTypeMap.Analyze(json);

        map.Fields.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_ArrayOfObjects_MergesItemFields()
    {
        var json = """{"items":[{"id":1,"name":"A"},{"id":2,"color":"red"}]}""";

        var map = JsonTypeMap.Analyze(json);

        map.Fields.Should().ContainKey("items");
        map.Fields["items"].JsonType.Should().Be(JsonFieldType.Array);
        var itemType = map.Fields["items"].ArrayItemType;
        itemType.Should().NotBeNull();
        itemType!.JsonType.Should().Be(JsonFieldType.Object);
        itemType.NestedFields.Should().NotBeNull();
        itemType.NestedFields!.Should().ContainKey("id");
        itemType.NestedFields!["id"].JsonType.Should().Be(JsonFieldType.Number);
        itemType.NestedFields!.Should().ContainKey("name");
        itemType.NestedFields!.Should().ContainKey("color");
    }
}
