using System.Text.Json;
using FluentAssertions;

namespace Iaet.DukeEnergy.Tests;

public class JsonPathLiteTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void SelectString_resolves_a_nested_property()
    {
        var root = Parse("""{"data":{"account":{"number":"12345"}}}""");

        JsonPathLite.SelectString(root, "data.account.number").Should().Be("12345");
    }

    [Fact]
    public void SelectString_resolves_through_array_indexers()
    {
        var root = Parse("""{"data":{"accounts":[{"number":"A"},{"number":"B"}]}}""");

        JsonPathLite.SelectString(root, "data.accounts[1].number").Should().Be("B");
    }

    [Fact]
    public void SelectString_coerces_numbers_and_booleans()
    {
        var root = Parse("""{"count":42,"active":true}""");

        JsonPathLite.SelectString(root, "count").Should().Be("42");
        JsonPathLite.SelectString(root, "active").Should().Be("true");
    }

    [Fact]
    public void SelectString_returns_null_for_a_path_that_does_not_resolve()
    {
        var root = Parse("""{"data":{"account":{}}}""");

        JsonPathLite.SelectString(root, "data.account.number").Should().BeNull();
        JsonPathLite.SelectString(root, "data.accounts[3].number").Should().BeNull();
        JsonPathLite.SelectString(root, "").Should().BeNull();
    }

    [Fact]
    public void TryGetPropertyIgnoreCase_matches_regardless_of_casing()
    {
        var root = Parse("""{"AccountNumber":"9"}""");

        JsonPathLite.TryGetPropertyIgnoreCase(root, "accountnumber", out var value).Should().BeTrue();
        JsonPathLite.AsString(value).Should().Be("9");
    }
}
