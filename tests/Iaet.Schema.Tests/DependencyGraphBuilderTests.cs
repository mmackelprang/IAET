using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class DependencyGraphBuilderTests
{
    [Fact]
    public void Build_detects_shared_ids_in_responses_and_requests()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/session", responseBody: """{"sessionId":"abc123"}"""),
            MakeRequest("GET", "/api/calls", requestHeaders: new() { ["X-Session-Id"] = "abc123" }),
        };

        var deps = DependencyGraphBuilder.Build(requests);

        deps.Should().Contain(d => d.From.Contains("/session") && d.To.Contains("/calls"));
    }

    [Fact]
    public void Build_detects_token_in_response_used_in_later_request_url()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/auth", responseBody: """{"token":"tok_xyz789"}"""),
            MakeRequest("GET", "/api/data?token=tok_xyz789"),
        };

        var deps = DependencyGraphBuilder.Build(requests);

        deps.Should().Contain(d => d.From.Contains("/auth") && d.To.Contains("/data"));
    }

    [Fact]
    public void Build_returns_empty_for_independent_requests()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/users"),
            MakeRequest("GET", "/api/products"),
        };

        var deps = DependencyGraphBuilder.Build(requests);

        deps.Should().BeEmpty();
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
