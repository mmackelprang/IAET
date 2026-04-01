using FluentAssertions;
using Iaet.Core.Models;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class CrossEndpointCorrelatorTests
{
    [Fact]
    public void Correlate_detects_value_in_request_header()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/auth", responseBody: """["token_abc123_xyz"]"""),
            MakeRequest("GET", "/api/data", requestHeaders: new() { ["Authorization"] = "token_abc123_xyz" }),
        };

        var result = CrossEndpointCorrelator.Correlate(requests);

        result.Should().Contain(c => c.SuggestedName == "authToken" && c.SourceEndpoint.Contains("/auth", StringComparison.Ordinal));
    }

    [Fact]
    public void Correlate_detects_value_in_request_body()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/session", responseBody: """["session_id_12345678"]"""),
            MakeRequest("POST", "/api/action", requestBody: """["session_id_12345678", "do_something"]"""),
        };

        var result = CrossEndpointCorrelator.Correlate(requests);

        result.Should().Contain(c =>
            c.SourceEndpoint.Contains("/session", StringComparison.Ordinal) &&
            c.ConsumedContext == "request_body");
    }

    [Fact]
    public void Correlate_detects_value_in_query_params()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/auth", responseBody: """["key_abcdef123456"]"""),
            MakeRequest("GET", "https://api.example.com/data?key=key_abcdef123456"),
        };

        var result = CrossEndpointCorrelator.Correlate(requests);

        result.Should().Contain(c => c.ConsumedContext == "request_url_query");
    }

    [Fact]
    public void Correlate_skips_short_values()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/a", responseBody: """["ab"]"""),
            MakeRequest("GET", "/api/b", requestHeaders: new() { ["X-Val"] = "ab" }),
        };

        var result = CrossEndpointCorrelator.Correlate(requests);
        result.Should().BeEmpty(); // "ab" is too short (< 6 chars)
    }

    [Fact]
    public void Correlate_skips_self_correlation()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/echo",
                responseBody: """["value_that_echoes_back"]""",
                requestBody: """["value_that_echoes_back"]"""),
        };

        // Need at least 2 requests for any correlation to happen
        var requests2 = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/echo",
                responseBody: """["value_that_echoes_back"]""",
                requestBody: """["value_that_echoes_back"]"""),
            MakeRequest("GET", "/api/other"),
        };

        var result = CrossEndpointCorrelator.Correlate(requests2);
        result.Should().BeEmpty(); // Same endpoint shouldn't self-correlate
    }

    [Fact]
    public void Correlate_handles_empty_input()
    {
        CrossEndpointCorrelator.Correlate([]).Should().BeEmpty();
    }

    [Fact]
    public void Correlate_handles_single_request()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/data", responseBody: """["some_value_here"]"""),
        };

        CrossEndpointCorrelator.Correlate(requests).Should().BeEmpty();
    }

    [Fact]
    public void CorrelateWithStreams_detects_value_in_sip_register()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/sipinfo", responseBody: """["web.c.pbx.voice.sip.google.com"]"""),
        };
        var streams = new List<CapturedStream>
        {
            MakeStream(StreamProtocol.WebSocket, [
                MakeFrame("REGISTER sip:web.c.pbx.voice.sip.google.com SIP/2.0\r\nVia: SIP/2.0/wss\r\n\r\n"),
            ]),
        };

        var result = CrossEndpointCorrelator.CorrelateWithStreams(requests, streams);

        result.Should().Contain(c => c.SuggestedName == "sipDomain" && c.Confidence == ConfidenceLevel.High);
    }

    [Fact]
    public void CorrelateWithStreams_detects_value_in_sip_invite()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/account", responseBody: """["+19196706660"]"""),
        };
        var streams = new List<CapturedStream>
        {
            MakeStream(StreamProtocol.WebSocket, [
                MakeFrame("INVITE sip:+19196706660@server SIP/2.0\r\n\r\n"),
            ]),
        };

        var result = CrossEndpointCorrelator.CorrelateWithStreams(requests, streams);

        result.Should().Contain(c => c.ConsumedContext == "sip_invite_target");
    }

    [Fact]
    public void CorrelateWithStreams_handles_no_streams()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/data", responseBody: """["some_value_here"]"""),
        };

        CrossEndpointCorrelator.CorrelateWithStreams(requests, []).Should().BeEmpty();
    }

    [Fact]
    public void Correlate_deduplicates_by_source_position()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/auth", responseBody: """["token_repeated_value"]"""),
            MakeRequest("GET", "/api/a", requestHeaders: new() { ["Authorization"] = "token_repeated_value" }),
            MakeRequest("GET", "/api/b", requestHeaders: new() { ["Authorization"] = "token_repeated_value" }),
        };

        var result = CrossEndpointCorrelator.Correlate(requests);

        // Should deduplicate — same source endpoint + position
        result.Where(c => c.SourceEndpoint.Contains("/auth", StringComparison.Ordinal)).Should().HaveCount(1);
    }

    [Fact]
    public void Correlate_truncates_long_values()
    {
        var longToken = new string('x', 100);
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/auth", responseBody: $"""["{longToken}"]"""),
            MakeRequest("GET", "/api/data", requestHeaders: new() { ["Authorization"] = longToken }),
        };

        var result = CrossEndpointCorrelator.Correlate(requests);

        result.Should().ContainSingle();
        result[0].Value.Should().HaveLength(40);
        result[0].Value.Should().EndWith("...");
    }

    [Fact]
    public void Correlate_throws_on_null_input()
    {
        var act = () => CrossEndpointCorrelator.Correlate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CorrelateWithStreams_throws_on_null_requests()
    {
        var act = () => CrossEndpointCorrelator.CorrelateWithStreams(null!, []);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CorrelateWithStreams_throws_on_null_streams()
    {
        var act = () => CrossEndpointCorrelator.CorrelateWithStreams([], null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static CapturedRequest MakeRequest(string method, string url,
        string? responseBody = null, string? requestBody = null,
        Dictionary<string, string>? requestHeaders = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"https://api.example.com{url}",
        RequestHeaders = requestHeaders ?? new Dictionary<string, string>(),
        RequestBody = requestBody,
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        ResponseBody = responseBody,
        DurationMs = 50,
    };

    private static CapturedStream MakeStream(StreamProtocol protocol, IReadOnlyList<StreamFrame> frames) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Protocol = protocol,
        Url = "wss://example.com/ws",
        StartedAt = DateTimeOffset.UtcNow,
        Metadata = new StreamMetadata(new Dictionary<string, string> { ["subprotocol"] = "sip" }),
        Frames = frames,
    };

    private static StreamFrame MakeFrame(string text) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Direction = StreamFrameDirection.Sent,
        TextPayload = text,
        SizeBytes = text.Length,
    };
}
