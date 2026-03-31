using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class GraphQlExtractorTests
{
    [Fact]
    public void Extract_finds_query_strings()
    {
        var js = """
            const QUERY = `query GetUser($id: ID!) { user(id: $id) { name email } }`;
            const MUTATION = "mutation CreateUser($input: UserInput!) { createUser(input: $input) { id } }";
            """;

        var queries = GraphQlExtractor.Extract(js);

        queries.Should().Contain(q => q.Contains("GetUser"));
        queries.Should().Contain(q => q.Contains("CreateUser"));
    }

    [Fact]
    public void Extract_handles_no_graphql()
    {
        GraphQlExtractor.Extract("var x = 1;").Should().BeEmpty();
    }
}
