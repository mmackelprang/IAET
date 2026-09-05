using System.Net;
using FluentAssertions;

namespace Iaet.DukeEnergy.Tests;

public class ConfigJsonCredentialProviderTests
{
    private const string ConfigJson = """
    { "consumer_key_emp": "key123", "consumer_secret_emp": "secret456", "other": "ignored" }
    """;

    [Fact]
    public async Task GetAuthorizationAsync_builds_a_basic_header_from_the_config_document()
    {
        var handler = new StubHttpMessageHandler().Respond("config.prod.json", ConfigJson);
        using var provider = new ConfigJsonCredentialProvider(new HttpClient(handler), new DukeEnergyOptions());

        var header = await provider.GetAuthorizationAsync();

        header.Scheme.Should().Be("Basic");
        header.Parameter.Should().Be(Convert.ToBase64String("key123:secret456"u8.ToArray()));
    }

    [Fact]
    public async Task GetAuthorizationAsync_caches_the_credentials()
    {
        var handler = new StubHttpMessageHandler().Respond("config.prod.json", ConfigJson);
        using var provider = new ConfigJsonCredentialProvider(new HttpClient(handler), new DukeEnergyOptions());

        await provider.GetAuthorizationAsync();
        await provider.GetAuthorizationAsync();

        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Invalidate_forces_a_refetch()
    {
        var handler = new StubHttpMessageHandler().Respond("config.prod.json", ConfigJson);
        using var provider = new ConfigJsonCredentialProvider(new HttpClient(handler), new DukeEnergyOptions());

        await provider.GetAuthorizationAsync();
        provider.Invalidate();
        await provider.GetAuthorizationAsync();

        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAuthorizationAsync_finds_credentials_nested_one_level_deep()
    {
        const string Nested = """{ "api": { "consumerKeyEmp": "k", "consumerSecretEmp": "s" } }""";
        var handler = new StubHttpMessageHandler().Respond("config.prod.json", Nested);
        using var provider = new ConfigJsonCredentialProvider(new HttpClient(handler), new DukeEnergyOptions());

        var header = await provider.GetAuthorizationAsync();

        header.Parameter.Should().Be(Convert.ToBase64String("k:s"u8.ToArray()));
    }

    [Fact]
    public async Task GetAuthorizationAsync_explains_the_failure_when_the_config_layout_changes()
    {
        var handler = new StubHttpMessageHandler().Respond("config.prod.json", """{"unrelated":"value"}""");
        using var provider = new ConfigJsonCredentialProvider(new HttpClient(handler), new DukeEnergyOptions());

        var act = async () => await provider.GetAuthorizationAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*consumer key/secret*");
    }

    [Fact]
    public async Task GetAuthorizationAsync_surfaces_http_failures()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("config.prod.json", "nope", HttpStatusCode.InternalServerError);
        using var provider = new ConfigJsonCredentialProvider(new HttpClient(handler), new DukeEnergyOptions());

        var act = async () => await provider.GetAuthorizationAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
