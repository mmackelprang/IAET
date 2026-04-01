using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class ApkAuthExtractorTests
{
    [Fact]
    public void Extract_finds_api_key_constants()
    {
        var java = """
            public class Config {
                public static final String API_KEY = "AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg";
                public static final String CLIENT_ID = "1234567890-abcdefg.apps.googleusercontent.com";
            }
            """;

        var auth = ApkAuthExtractor.Extract(java, "Config.java");

        auth.Should().Contain(a => a.Key == "API_KEY" && a.Value.StartsWith("AIza", StringComparison.Ordinal));
        auth.Should().Contain(a => a.Key == "CLIENT_ID");
    }

    [Fact]
    public void Extract_finds_header_construction()
    {
        var java = """
            request.addHeader("Authorization", "Bearer " + token);
            request.addHeader("X-Api-Key", apiKey);
            """;

        var auth = ApkAuthExtractor.Extract(java, "Client.java");

        auth.Should().Contain(a => a.Key == "Authorization");
        auth.Should().Contain(a => a.Key == "X-Api-Key");
    }

    [Fact]
    public void Extract_works_with_obfuscated_constants()
    {
        var java = """
            public class a {
                static final String b = "AIzaSyBGb5fGAyC-pRcRU6MUHb__b_vKha71HRE";
            }
            """;

        var auth = ApkAuthExtractor.Extract(java, "a.java");

        auth.Should().Contain(a => a.Value.StartsWith("AIza", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_handles_empty()
    {
        ApkAuthExtractor.Extract("", "empty.java").Should().BeEmpty();
    }
}
