using FluentAssertions;
using Iaet.Capture;

namespace Iaet.Capture.Tests;

public class RequestSanitizerTests
{
    [Fact]
    public void SanitizeHeaders_RedactsAuthorizationHeader()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret-token-123",
            ["Content-Type"] = "application/json"
        };
        var result = RequestSanitizer.SanitizeHeaders(headers);
        result["Authorization"].Should().Be("<REDACTED>");
        result["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void SanitizeHeaders_RedactsCookies()
    {
        var headers = new Dictionary<string, string>
        {
            ["Cookie"] = "session=abc123",
            ["Accept"] = "text/html"
        };
        var result = RequestSanitizer.SanitizeHeaders(headers);
        result["Cookie"].Should().Be("<REDACTED>");
        result["Accept"].Should().Be("text/html");
    }

    [Fact]
    public void SanitizeHeaders_RedactsAllSensitiveHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["authorization"] = "secret",
            ["cookie"] = "secret",
            ["set-cookie"] = "secret",
            ["x-goog-authuser"] = "secret",
            ["x-csrf-token"] = "secret",
            ["x-xsrf-token"] = "secret",
            ["user-agent"] = "Mozilla/5.0"
        };
        var result = RequestSanitizer.SanitizeHeaders(headers);
        result.Where(kv => kv.Value == "<REDACTED>").Should().HaveCount(6);
        result["user-agent"].Should().Be("Mozilla/5.0");
    }

    [Fact]
    public void SanitizeHeaders_IsCaseInsensitive()
    {
        var headers = new Dictionary<string, string>
        {
            ["AUTHORIZATION"] = "Bearer token",
            ["COOKIE"] = "session=abc"
        };
        var result = RequestSanitizer.SanitizeHeaders(headers);
        result["AUTHORIZATION"].Should().Be("<REDACTED>");
        result["COOKIE"].Should().Be("<REDACTED>");
    }

    [Fact]
    public void SanitizeHeaders_EmptyDictionary_ReturnsEmpty()
    {
        var result = RequestSanitizer.SanitizeHeaders(new Dictionary<string, string>());
        result.Should().BeEmpty();
    }
}
