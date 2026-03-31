using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class ApkUrlExtractorTests
{
    [Fact]
    public void Extract_finds_string_literal_urls()
    {
        var java = """
            public class ApiClient {
                private static final String BASE_URL = "https://api.example.com/v2";
                private static final String WS_URL = "wss://ws.example.com/realtime";
            }
            """;

        var urls = ApkUrlExtractor.Extract(java, "ApiClient.java");

        urls.Should().Contain(u => u.Url == "https://api.example.com/v2");
        urls.Should().Contain(u => u.Url == "wss://ws.example.com/realtime");
    }

    [Fact]
    public void Extract_finds_retrofit_annotations()
    {
        var java = """
            public interface VoiceApi {
                @GET("users/{id}")
                Call<User> getUser(@Path("id") String userId);

                @POST("messages")
                Call<Message> sendMessage(@Body MessageRequest request);

                @DELETE("threads/{threadId}")
                Call<Void> deleteThread(@Path("threadId") String threadId);
            }
            """;

        var urls = ApkUrlExtractor.Extract(java, "VoiceApi.java");

        urls.Should().Contain(u => u.Url == "users/{id}" && u.HttpMethod == "GET");
        urls.Should().Contain(u => u.Url == "messages" && u.HttpMethod == "POST");
        urls.Should().Contain(u => u.Url == "threads/{threadId}" && u.HttpMethod == "DELETE");
    }

    [Fact]
    public void Extract_finds_okhttp_urls()
    {
        var java = """
            Request request = new Request.Builder()
                .url("https://clients6.google.com/voice/v1/voiceclient/api")
                .build();
            """;

        var urls = ApkUrlExtractor.Extract(java, "Service.java");

        urls.Should().Contain(u => u.Url == "https://clients6.google.com/voice/v1/voiceclient/api");
    }

    [Fact]
    public void Extract_works_with_obfuscated_code()
    {
        var java = """
            public class a {
                private static final String b = "https://api.secret.com/v1/data";
                public void c() {
                    new Request.Builder().url("https://api.secret.com/v1/users").build();
                }
            }
            """;

        var urls = ApkUrlExtractor.Extract(java, "a.java");

        urls.Should().Contain(u => u.Url == "https://api.secret.com/v1/data");
        urls.Should().Contain(u => u.Url == "https://api.secret.com/v1/users");
    }

    [Fact]
    public void Extract_ignores_non_api_urls()
    {
        var java = """
            String icon = "https://example.com/icon.png";
            String stylesheet = "https://example.com/style.css";
            String api = "https://example.com/api/v1/data";
            """;

        var urls = ApkUrlExtractor.Extract(java, "App.java");

        urls.Should().NotContain(u => u.Url.Contains(".png"));
        urls.Should().NotContain(u => u.Url.Contains(".css"));
        urls.Should().Contain(u => u.Url.Contains("/api/v1/data"));
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        ApkUrlExtractor.Extract("", "empty.java").Should().BeEmpty();
    }

    [Fact]
    public void ExtractFromDirectory_scans_all_java_files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Api.java"),
                """private static final String URL = "https://api.example.com/v1";""");
            File.WriteAllText(Path.Combine(tempDir, "Other.java"),
                """String x = "not a url";""");

            var urls = ApkUrlExtractor.ExtractFromDirectory(tempDir);

            urls.Should().Contain(u => u.Url == "https://api.example.com/v1");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
