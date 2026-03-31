using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class UrlExtractorTests
{
    [Fact]
    public void Extract_finds_absolute_api_urls()
    {
        var js = """
            const baseUrl = "https://clients6.google.com/voice/v1/voiceclient";
            fetch("https://voice.google.com/api/v2/calls");
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "https://clients6.google.com/voice/v1/voiceclient");
        urls.Should().Contain(u => u.Url == "https://voice.google.com/api/v2/calls");
    }

    [Fact]
    public void Extract_finds_relative_api_paths()
    {
        var js = """
            fetch("/api/v1/users");
            xhr.open("GET", "/api/v1/messages");
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "/api/v1/users");
        urls.Should().Contain(u => u.Url == "/api/v1/messages");
    }

    [Fact]
    public void Extract_ignores_non_api_paths()
    {
        var js = """
            import "/static/styles.css";
            const img = "/images/logo.png";
            const path = "/api/v1/data";
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().NotContain(u => u.Url.Contains(".css"));
        urls.Should().NotContain(u => u.Url.Contains(".png"));
        urls.Should().Contain(u => u.Url == "/api/v1/data");
    }

    [Fact]
    public void Extract_reports_line_numbers()
    {
        var js = "line1\nfetch(\"/api/test\")\nline3";

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "/api/test" && u.LineNumber == 2);
    }

    [Fact]
    public void Extract_includes_source_context()
    {
        var js = """fetch("/api/v1/users")""";

        var urls = UrlExtractor.Extract(js, "main.js");

        urls.Should().Contain(u => u.SourceFile == "main.js");
    }

    [Fact]
    public void Extract_deduplicates_urls()
    {
        var js = """
            fetch("/api/v1/users");
            fetch("/api/v1/users");
            fetch("/api/v1/users");
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Where(u => u.Url == "/api/v1/users").Should().HaveCount(1);
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        var urls = UrlExtractor.Extract("", "bundle.js");
        urls.Should().BeEmpty();
    }
}
