using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class RateLimitDetectorTests
{
    [Fact]
    public void Detect_finds_429_responses()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/users", 200),
            MakeRequest("GET", "/api/users", 429, responseHeaders: new() { ["Retry-After"] = "30" }),
            MakeRequest("GET", "/api/users", 200),
        };

        var result = RateLimitDetector.Detect(requests);

        result.Should().Contain(r => r.Endpoint.Contains("/users") && r.RetryAfterSeconds == 30);
    }

    [Fact]
    public void Detect_returns_empty_for_no_429s()
    {
        var requests = new List<CapturedRequest> { MakeRequest("GET", "/api/data", 200) };

        RateLimitDetector.Detect(requests).Should().BeEmpty();
    }

    [Fact]
    public void Detect_handles_missing_retry_after()
    {
        var requests = new List<CapturedRequest> { MakeRequest("GET", "/api/data", 429) };

        var result = RateLimitDetector.Detect(requests);

        result.Should().HaveCount(1);
        result[0].RetryAfterSeconds.Should().BeNull();
    }

    private static CapturedRequest MakeRequest(string method, string url, int status, Dictionary<string, string>? responseHeaders = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = $"https://example.com{url}",
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = status,
        ResponseHeaders = responseHeaders ?? new Dictionary<string, string>(),
        DurationMs = 50,
    };
}
