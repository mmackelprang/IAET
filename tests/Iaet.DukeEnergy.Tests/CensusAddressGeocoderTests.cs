using System.Net;
using FluentAssertions;

namespace Iaet.DukeEnergy.Tests;

public class CensusAddressGeocoderTests
{
    private const string MatchJson = """
    {
      "result": {
        "input": { "address": { "address": "425 Stadium Dr, Tuscaloosa, AL 35401" } },
        "addressMatches": [
          {
            "matchedAddress": "425 STADIUM DR, TUSCALOOSA, AL, 35401",
            "coordinates": { "x": -87.549700416257, "y": 33.21105403378 },
            "addressComponents": { "city": "TUSCALOOSA", "state": "AL", "zip": "35401" }
          }
        ]
      }
    }
    """;

    private const string NoMatchJson = """{ "result": { "addressMatches": [] } }""";

    [Fact]
    public async Task GeocodeAsync_reads_the_first_match_with_x_as_longitude_and_y_as_latitude()
    {
        var handler = new StubHttpMessageHandler().Respond("onelineaddress", MatchJson);
        var geocoder = new CensusAddressGeocoder(new HttpClient(handler), new DukeEnergyOptions());

        var result = await geocoder.GeocodeAsync("425 Stadium Dr, Tuscaloosa, AL 35401");

        result.Should().NotBeNull();
        result!.Point.Latitude.Should().BeApproximately(33.21105403378, 1e-9);
        result.Point.Longitude.Should().BeApproximately(-87.549700416257, 1e-9);
        result.MatchedAddress.Should().Be("425 STADIUM DR, TUSCALOOSA, AL, 35401");
        result.InputAddress.Should().Be("425 Stadium Dr, Tuscaloosa, AL 35401");
        result.Source.Should().Be("census");
    }

    [Fact]
    public async Task GeocodeAsync_sends_the_address_and_benchmark_as_query_parameters()
    {
        var handler = new StubHttpMessageHandler().Respond("onelineaddress", MatchJson);
        var geocoder = new CensusAddressGeocoder(new HttpClient(handler), new DukeEnergyOptions());

        await geocoder.GeocodeAsync("123 Main St, Raleigh, NC");

        var uri = handler.Requests[0].RequestUri!;
        uri.AbsolutePath.Should().Be("/geocoder/locations/onelineaddress");

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["address"].Should().Be("123 Main St, Raleigh, NC");
        query["benchmark"].Should().Be("Public_AR_Current");
        query["format"].Should().Be("json");

        // The wire form must stay escaped — a raw space in a request line is invalid.
        uri.AbsoluteUri.Should().NotContain(" ");
    }

    [Fact]
    public async Task GeocodeAsync_returns_null_when_nothing_matches()
    {
        var handler = new StubHttpMessageHandler().Respond("onelineaddress", NoMatchJson);
        var geocoder = new CensusAddressGeocoder(new HttpClient(handler), new DukeEnergyOptions());

        (await geocoder.GeocodeAsync("nowhere at all")).Should().BeNull();
    }

    [Fact]
    public async Task GeocodeAsync_caches_results_including_misses()
    {
        var handler = new StubHttpMessageHandler().Respond("onelineaddress", NoMatchJson);
        var geocoder = new CensusAddressGeocoder(new HttpClient(handler), new DukeEnergyOptions());

        await geocoder.GeocodeAsync("nowhere");
        await geocoder.GeocodeAsync("nowhere");

        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task GeocodeAsync_surfaces_http_failures()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("onelineaddress", "boom", HttpStatusCode.ServiceUnavailable);
        var geocoder = new CensusAddressGeocoder(new HttpClient(handler), new DukeEnergyOptions());

        var act = async () => await geocoder.GeocodeAsync("123 Main St");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void Parse_returns_null_for_malformed_or_incomplete_payloads()
    {
        CensusAddressGeocoder.Parse("x", "not json").Should().BeNull();
        CensusAddressGeocoder.Parse("x", "").Should().BeNull();
        CensusAddressGeocoder.Parse("x", """{"result":{}}""").Should().BeNull();
        CensusAddressGeocoder.Parse("x", """{"result":{"addressMatches":[{"matchedAddress":"A"}]}}""")
            .Should().BeNull("a match without coordinates is unusable");
    }
}
