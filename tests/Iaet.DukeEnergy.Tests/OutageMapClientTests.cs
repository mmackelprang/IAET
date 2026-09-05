using System.Net;
using FluentAssertions;
using Iaet.DukeEnergy.Abstractions;

namespace Iaet.DukeEnergy.Tests;

public class OutageMapClientTests
{
    private const string OutagesJson = """
    {
      "data": [
        { "id": "near",   "latitude": 35.7800, "longitude": -78.6400, "customersAffected": 10 },
        { "id": "closer", "latitude": 35.7797, "longitude": -78.6383, "customersAffected": 4 },
        { "id": "far",    "latitude": 36.5000, "longitude": -79.5000, "customersAffected": 99 },
        { "id": "nogeo",  "customersAffected": 1 }
      ]
    }
    """;

    private static FakeCredentialProvider Credentials() => new();

    private static OutageMapClient CreateClient(StubHttpMessageHandler handler, DukeEnergyOptions? options = null)
        => new(new HttpClient(handler), options ?? new DukeEnergyOptions(), Credentials());

    [Fact]
    public async Task GetOutagesAsync_sends_the_jurisdiction_and_browser_headers()
    {
        var handler = new StubHttpMessageHandler().Respond("/outages", OutagesJson);
        var client  = CreateClient(handler);

        await client.GetOutagesAsync(Jurisdictions.Florida);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.ToString().Should().Contain("jurisdiction=DEF");
        request.RequestUri.ToString().Should().StartWith("https://cust-api.duke-energy.com/outage-maps/v1/outages");
        request.Headers.Authorization!.Scheme.Should().Be("Basic");
        request.Headers.GetValues("Origin").Should().ContainSingle("https://outagemap.duke-energy.com");
    }

    [Fact]
    public async Task GetOutagesAsync_falls_back_to_the_configured_jurisdiction()
    {
        var handler = new StubHttpMessageHandler().Respond("/outages", OutagesJson);
        var options = new DukeEnergyOptions { Jurisdiction = Jurisdictions.Indiana };

        await CreateClient(handler, options).GetOutagesAsync();

        handler.Requests[0].RequestUri!.ToString().Should().Contain("jurisdiction=DEI");
    }

    [Fact]
    public async Task GetNeighborhoodAsync_keeps_only_outages_inside_the_radius_nearest_first()
    {
        var handler = new StubHttpMessageHandler().Respond("/outages", OutagesJson);
        var client  = CreateClient(handler);

        var report = await client.GetNeighborhoodAsync(35.7796, -78.6382, 1.0);

        report.Outages.Select(o => o.Id).Should().Equal("closer", "near");
        report.OutageCount.Should().Be(2);
        report.CustomersAffected.Should().Be(14);
        report.NearestOutageMiles.Should().BeLessThan(0.1);
        report.Center.Latitude.Should().Be(35.7796);
        report.RadiusMiles.Should().Be(1.0);
    }

    [Fact]
    public async Task GetNeighborhoodAsync_returns_an_empty_report_when_nothing_is_nearby()
    {
        var handler = new StubHttpMessageHandler().Respond("/outages", OutagesJson);

        var report = await CreateClient(handler).GetNeighborhoodAsync(0, 0, 5);

        report.OutageCount.Should().Be(0);
        report.CustomersAffected.Should().Be(0);
        report.NearestOutageMiles.Should().BeNull();
    }

    [Fact]
    public async Task GetNeighborhoodAsync_rejects_a_non_positive_radius()
    {
        var handler = new StubHttpMessageHandler().Respond("/outages", OutagesJson);

        var act = async () => await CreateClient(handler).GetNeighborhoodAsync(1, 1, 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Responses_are_cached_for_the_configured_duration()
    {
        var handler = new StubHttpMessageHandler().Respond("/outages", OutagesJson);
        var client  = CreateClient(handler);

        await client.GetOutagesAsync();
        await client.GetOutagesAsync();

        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task An_expired_cache_entry_triggers_a_refetch()
    {
        var handler = new StubHttpMessageHandler().Respond("/outages", OutagesJson);
        var clock   = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var options = new DukeEnergyOptions { OutageCacheDuration = TimeSpan.FromMinutes(2) };
        var client  = new OutageMapClient(new HttpClient(handler), options, Credentials(), clock);

        await client.GetOutagesAsync();
        clock.Advance(TimeSpan.FromMinutes(3));
        await client.GetOutagesAsync();

        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_rejected_credential_is_invalidated_and_the_request_retried_once()
    {
        var handler     = new StubHttpMessageHandler();
        var credentials = Credentials();

        // First call is rejected; the stub then serves the payload for the retry.
        handler.Respond("/outages", "denied", HttpStatusCode.Unauthorized);
        var client = new OutageMapClient(new HttpClient(handler), new DukeEnergyOptions(), credentials);

        var act = async () => await client.GetOutagesAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
        credentials.InvalidateCount.Should().Be(1);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCountiesAsync_targets_the_counties_resource()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/counties", """{"data":[{"countyName":"Wake","state":"NC","customersServed":10,"areaOfInterestSummary":{"maxCustomersAffected":1}}]}""");

        var counties = await CreateClient(handler).GetCountiesAsync();

        counties.Should().ContainSingle();
        counties[0].CountyName.Should().Be("Wake");
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/outage-maps/v1/counties");
    }
}
