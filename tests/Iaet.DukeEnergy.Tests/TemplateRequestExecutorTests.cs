using FluentAssertions;
using Iaet.DukeEnergy.Profiles;

namespace Iaet.DukeEnergy.Tests;

public class TemplateRequestExecutorTests
{
    private static readonly OutageReportProfile Profile = new(
        BaseUri: new Uri("https://example.test"),
        DefaultHeaders: new Dictionary<string, string> { ["X-Default"] = "yes", ["Accept"] = "application/json" });

    [Fact]
    public async Task ExecuteAsync_renders_url_body_and_headers_then_maps_the_response()
    {
        var handler = new StubHttpMessageHandler().Respond(
            "/find",
            """{"result":{"accounts":[{"id":"ACC-9","address":"1 Main St"}]}}""");

        var template = new RequestTemplate(
            "POST",
            "/find?phone={{phoneNumber}}",
            Headers: new Dictionary<string, string> { ["X-Trace"] = "{{phoneNumber}}" },
            Body: """{"phone":"{{phoneNumber}}","note":"{{comments}}"}""",
            ResponseMap: new Dictionary<string, string>
            {
                ["accountNumber"]  = "result.accounts[0].id",
                ["serviceAddress"] = "result.accounts[0].address",
            });

        var executor = new TemplateRequestExecutor(new HttpClient(handler));

        var response = await executor.ExecuteAsync(
            template,
            Profile,
            new Dictionary<string, string?> { ["phoneNumber"] = "9195550100", ["comments"] = "he said \"out\"" });

        response.IsSuccess.Should().BeTrue();
        response.Field("accountNumber").Should().Be("ACC-9");
        response.Field("serviceAddress").Should().Be("1 Main St");

        var request = handler.Requests[0];
        request.RequestUri!.ToString().Should().Be("https://example.test/find?phone=9195550100");
        request.Headers.GetValues("X-Trace").Should().ContainSingle("9195550100");
        request.Headers.GetValues("X-Default").Should().ContainSingle("yes");

        // The quotes in the comment must be escaped so the rendered body is still parseable JSON
        // that round-trips the original text.
        using var body = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[0]);
        body.RootElement.GetProperty("phone").GetString().Should().Be("9195550100");
        body.RootElement.GetProperty("note").GetString().Should().Be("he said \"out\"");
    }

    [Fact]
    public void Render_url_encodes_substituted_values()
    {
        var template = new RequestTemplate("GET", "/lookup?q={{value}}");

        using var request = TemplateRequestExecutor.Render(
            template,
            Profile,
            new Dictionary<string, string?> { ["value"] = "a b&c" });

        request.RequestUri!.Query.Should().Be("?q=a%20b%26c");
    }

    [Fact]
    public void Render_resolves_env_tokens_from_the_environment()
    {
        Environment.SetEnvironmentVariable("IAET_DUKE_TEST_TOKEN", "Bearer abc");
        try
        {
            var template = new RequestTemplate(
                "GET",
                "/x",
                Headers: new Dictionary<string, string> { ["Authorization"] = "{{env:IAET_DUKE_TEST_TOKEN}}" });

            using var request = TemplateRequestExecutor.Render(
                template,
                Profile,
                new Dictionary<string, string?>());

            request.Headers.Authorization!.ToString().Should().Be("Bearer abc");
        }
        finally
        {
            Environment.SetEnvironmentVariable("IAET_DUKE_TEST_TOKEN", null);
        }
    }

    [Fact]
    public void Render_substitutes_an_unknown_token_with_an_empty_string()
    {
        var template = new RequestTemplate("GET", "/x?v={{missing}}");

        using var request = TemplateRequestExecutor.Render(template, Profile, new Dictionary<string, string?>());

        request.RequestUri!.Query.Should().Be("?v=");
    }

    [Fact]
    public void Render_rejects_a_relative_url_when_the_profile_has_no_base_uri()
    {
        var template = new RequestTemplate("GET", "/x");

        var act = () => TemplateRequestExecutor.Render(template, new OutageReportProfile(), new Dictionary<string, string?>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*baseUri*");
    }

    [Fact]
    public async Task ExecuteAsync_returns_no_fields_when_the_response_is_not_json()
    {
        var handler  = new StubHttpMessageHandler().Respond("/x", "<html>maintenance</html>");
        var template = new RequestTemplate(
            "GET",
            "/x",
            ResponseMap: new Dictionary<string, string> { ["accountNumber"] = "a.b" });

        var response = await new TemplateRequestExecutor(new HttpClient(handler))
            .ExecuteAsync(template, Profile, new Dictionary<string, string?>());

        response.Fields.Should().BeEmpty();
        response.Field("accountNumber").Should().BeNull();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("False", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("Y", true)]
    [InlineData("no", false)]
    [InlineData("maybe", null)]
    public void Flag_parses_common_truthy_spellings(string value, bool? expected)
    {
        var response = new TemplateResponse(
            200,
            true,
            "{}",
            new Dictionary<string, string?> { ["f"] = value });

        response.Flag("f").Should().Be(expected);
    }

    [Fact]
    public void Timestamp_parses_iso_and_epoch_values()
    {
        var response = new TemplateResponse(
            200,
            true,
            "{}",
            new Dictionary<string, string?>
            {
                ["iso"]    = "2026-09-05T14:30:00Z",
                ["millis"] = "1757082600000",
            });

        response.Timestamp("iso").Should().Be(new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero));
        response.Timestamp("millis").Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1757082600000));
        response.Timestamp("absent").Should().BeNull();
    }
}
