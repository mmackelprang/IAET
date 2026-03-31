using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class ConfigExtractorTests
{
    [Fact]
    public void Extract_finds_api_base_urls()
    {
        var js = """
            const API_BASE = "https://api.example.com/v2";
            const config = { apiUrl: "https://voice.google.com/api", timeout: 5000 };
            """;

        var configs = ConfigExtractor.Extract(js);

        configs.Should().Contain(c => c.Key == "API_BASE" && c.Value == "https://api.example.com/v2");
    }

    [Fact]
    public void Extract_finds_feature_flags()
    {
        var js = """
            const ENABLE_WEBRTC = true;
            const FEATURE_FLAGS = { enableSms: false, enableVoip: true };
            """;

        var configs = ConfigExtractor.Extract(js);

        configs.Should().Contain(c => c.Key == "ENABLE_WEBRTC");
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        ConfigExtractor.Extract("").Should().BeEmpty();
    }
}
