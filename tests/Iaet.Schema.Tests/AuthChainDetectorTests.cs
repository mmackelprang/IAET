using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class AuthChainDetectorTests
{
    [Fact]
    public void Detect_finds_auth_header_chains()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/login", responseBody: """{"access_token":"eyJ.payload.sig"}"""),
            MakeRequest("GET", "/api/data", requestHeaders: new() { ["Authorization"] = "<REDACTED>" }),
        };

        var chains = AuthChainDetector.Detect(requests);

        chains.Should().NotBeEmpty();
        chains[0].Steps.Should().Contain(s => s.Endpoint.Contains("/login"));
    }

    [Fact]
    public void Detect_returns_empty_when_no_auth_patterns()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/public/data"),
        };

        var chains = AuthChainDetector.Detect(requests);

        chains.Should().BeEmpty();
    }

    private static CapturedRequest MakeRequest(string method, string url, string? responseBody = null, Dictionary<string, string>? requestHeaders = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = $"https://example.com{url}",
        RequestHeaders = requestHeaders ?? new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        ResponseBody = responseBody,
        DurationMs = 50,
    };
}
