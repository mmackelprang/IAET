using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class JsonTypeMapRobustnessTests
{
    [Fact]
    public void TryAnalyze_handles_jsonp_prefix()
    {
        var result = JsonTypeMap.TryAnalyze(""");}\n{"key":"val"}""");
        result.Should().BeNull();
    }

    [Fact]
    public void TryAnalyze_handles_html_response()
    {
        var result = JsonTypeMap.TryAnalyze("<!DOCTYPE html><html><body>Error</body></html>");
        result.Should().BeNull();
    }

    [Fact]
    public void TryAnalyze_handles_bom_prefix()
    {
        var result = JsonTypeMap.TryAnalyze("\uFEFF{\"key\":\"val\"}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void TryAnalyze_handles_xss_protection_prefix()
    {
        var result = JsonTypeMap.TryAnalyze(")]}'\\n{\"key\":\"val\"}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void TryAnalyze_handles_empty_string()
    {
        JsonTypeMap.TryAnalyze("").Should().BeNull();
    }

    [Fact]
    public void TryAnalyze_handles_null()
    {
        JsonTypeMap.TryAnalyze(null!).Should().BeNull();
    }
}
