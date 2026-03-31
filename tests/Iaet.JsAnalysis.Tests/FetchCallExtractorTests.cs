using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class FetchCallExtractorTests
{
    [Fact]
    public void Extract_finds_fetch_calls_with_method()
    {
        var js = """
            fetch("/api/users", { method: "POST", body: JSON.stringify(data) });
            fetch("/api/sessions", { method: "DELETE" });
            """;

        var calls = FetchCallExtractor.Extract(js, "app.js");

        calls.Should().Contain(u => u.Url == "/api/users" && u.HttpMethod == "POST");
        calls.Should().Contain(u => u.Url == "/api/sessions" && u.HttpMethod == "DELETE");
    }

    [Fact]
    public void Extract_defaults_to_GET_when_no_method()
    {
        var js = """fetch("/api/data")""";

        var calls = FetchCallExtractor.Extract(js, "app.js");

        calls.Should().Contain(u => u.Url == "/api/data" && u.HttpMethod == "GET");
    }

    [Fact]
    public void Extract_finds_xhr_open_calls()
    {
        var js = """xhr.open("PUT", "/api/users/123")""";

        var calls = FetchCallExtractor.Extract(js, "app.js");

        calls.Should().Contain(u => u.Url == "/api/users/123" && u.HttpMethod == "PUT");
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        FetchCallExtractor.Extract("", "x.js").Should().BeEmpty();
    }
}
