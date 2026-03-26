using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.Replay;
using NSubstitute;

namespace Iaet.Replay.Tests;

public class HttpReplayEngineTests
{
    // ------------------------------------------------------------------ helpers

    private static CapturedRequest MakeCapturedRequest(
        string method = "GET",
        string url = "https://example.com/api/data",
        string? requestBody = null,
        int responseStatus = 200,
        string? responseBody = null,
        Dictionary<string, string>? requestHeaders = null,
        Dictionary<string, string>? responseHeaders = null)
    {
        return new CapturedRequest
        {
            Id              = Guid.NewGuid(),
            SessionId       = Guid.NewGuid(),
            Timestamp       = DateTimeOffset.UtcNow,
            HttpMethod      = method,
            Url             = url,
            RequestHeaders  = requestHeaders  ?? [],
            RequestBody     = requestBody,
            ResponseStatus  = responseStatus,
            ResponseHeaders = responseHeaders ?? [],
            ResponseBody    = responseBody,
            DurationMs      = 100,
        };
    }

    private static (HttpReplayEngine engine, FakeHttpHandler handler) CreateEngine(
        ReplayOptions? options = null,
        IReplayAuthProvider? authProvider = null,
        HttpStatusCode responseStatusCode = HttpStatusCode.OK,
        string? responseContent = null)
    {
        var handler = new FakeHttpHandler(responseStatusCode, responseContent);
        // HttpClient ownership transfers to HttpReplayEngine which is IDisposable;
        // tests dispose the engine, which covers the client lifetime.
        var client = new HttpClient(handler);
        try
        {
            var engine = new HttpReplayEngine(client, options ?? new ReplayOptions(), authProvider);
            return (engine, handler);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task ReplayAsync_MatchingResponse_ReturnsNoDiffs()
    {
        var body = """{"id":1,"name":"Alice"}""";
        var original = MakeCapturedRequest(responseStatus: 200, responseBody: body);
        var (engine, _) = CreateEngine(responseStatusCode: HttpStatusCode.OK, responseContent: body);

        var result = await engine.ReplayAsync(original);

        result.ResponseStatus.Should().Be(200);
        result.Diffs.Should().BeEmpty();
        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ReplayAsync_DifferentResponse_ReportsDiffs()
    {
        var original = MakeCapturedRequest(
            responseStatus: 200,
            responseBody: """{"name":"Alice"}""");
        var (engine, _) = CreateEngine(
            responseStatusCode: HttpStatusCode.OK,
            responseContent: """{"name":"Bob"}""");

        var result = await engine.ReplayAsync(original);

        result.Diffs.Should().ContainSingle()
            .Which.Path.Should().Be("$.name");
    }

    [Fact]
    public async Task ReplayAsync_DryRun_DoesNotSendRequest()
    {
        var original = MakeCapturedRequest();
        var options  = new ReplayOptions { DryRun = true };
        var (engine, handler) = CreateEngine(options);

        var result = await engine.ReplayAsync(original);

        handler.CallCount.Should().Be(0);
        result.ResponseStatus.Should().Be(0);
        result.ResponseBody.Should().BeNull();
        result.Diffs.Should().BeEmpty();
        result.DurationMs.Should().Be(0);
    }

    [Fact]
    public async Task ReplayAsync_WithAuthProvider_CallsApplyAuthAsync()
    {
        var original     = MakeCapturedRequest();
        var authProvider = Substitute.For<IReplayAuthProvider>();
        var (engine, _)  = CreateEngine(authProvider: authProvider);

        await engine.ReplayAsync(original);

        await authProvider.Received(1)
            .ApplyAuthAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
    }
}

// ---------------------------------------------------------------------------

/// <summary>A simple <see cref="HttpMessageHandler"/> that returns a canned response.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string?        _content;

    public int CallCount { get; private set; }

    public FakeHttpHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string? content = null)
    {
        _statusCode = statusCode;
        _content    = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        var response = new HttpResponseMessage(_statusCode);
        if (_content is not null)
        {
            response.Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json");
        }

        return Task.FromResult(response);
    }
}
