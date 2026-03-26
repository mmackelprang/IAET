using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Replay;

namespace Iaet.Replay.Tests;

public class JsonDifferTests
{
    [Fact]
    public void Diff_IdenticalJson_ReturnsNoDiffs()
    {
        var json = """{"name":"Alice","age":30}""";

        var diffs = JsonDiffer.Diff(json, json);

        diffs.Should().BeEmpty();
    }

    [Fact]
    public void Diff_ChangedValue_ReportsChangedField()
    {
        var expected = """{"name":"Alice","age":30}""";
        var actual   = """{"name":"Alice","age":31}""";

        var diffs = JsonDiffer.Diff(expected, actual);

        diffs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldDiff("$.age", "30", "31"));
    }

    [Fact]
    public void Diff_AddedField_ReportsFieldWithNullExpected()
    {
        var expected = """{"name":"Alice"}""";
        var actual   = """{"name":"Alice","extra":"bonus"}""";

        var diffs = JsonDiffer.Diff(expected, actual);

        diffs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldDiff("$.extra", null, "\"bonus\""));
    }

    [Fact]
    public void Diff_RemovedField_ReportsFieldWithNullActual()
    {
        var expected = """{"name":"Alice","extra":"bonus"}""";
        var actual   = """{"name":"Alice"}""";

        var diffs = JsonDiffer.Diff(expected, actual);

        diffs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldDiff("$.extra", "\"bonus\"", null));
    }

    [Fact]
    public void Diff_NestedChange_ReportsFullJsonPath()
    {
        var expected = """{"user":{"name":"Alice","age":30}}""";
        var actual   = """{"user":{"name":"Bob","age":30}}""";

        var diffs = JsonDiffer.Diff(expected, actual);

        diffs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldDiff("$.user.name", "\"Alice\"", "\"Bob\""));
    }

    [Fact]
    public void Diff_BothNull_ReturnsEmpty()
    {
        var diffs = JsonDiffer.Diff(null, null);

        diffs.Should().BeEmpty();
    }

    [Fact]
    public void Diff_OneNull_ReportsAllFieldsAsDiff()
    {
        var json = """{"name":"Alice","age":30}""";

        var diffs = JsonDiffer.Diff(json, null);

        diffs.Should().HaveCount(2);
        diffs.Should().Contain(d => d.Path == "$.name" && d.Expected == "\"Alice\"" && d.Actual == null);
        diffs.Should().Contain(d => d.Path == "$.age"  && d.Expected == "30"        && d.Actual == null);
    }
}
