using FluentAssertions;

namespace Iaet.DukeEnergy.Tests;

public class OutageJsonParserTests
{
    [Fact]
    public void ParseCounties_reads_the_documented_county_payload_shape()
    {
        const string Json = """
        {
          "data": [
            {
              "state": "NC",
              "countyName": "Wake",
              "customersServed": 100000,
              "areaOfInterestSummary": { "maxCustomersAffected": 2500, "outageCount": 12 }
            },
            {
              "state": "SC",
              "countyName": "York",
              "customersServed": 50000,
              "areaOfInterestSummary": { "maxCustomersAffected": 0 }
            }
          ]
        }
        """;

        var counties = OutageJsonParser.ParseCounties(Json);

        counties.Should().HaveCount(2);
        counties[0].CountyName.Should().Be("Wake");
        counties[0].State.Should().Be("NC");
        counties[0].CustomersServed.Should().Be(100000);
        counties[0].CustomersAffected.Should().Be(2500);
        counties[0].OutageCount.Should().Be(12);
        counties[0].PercentAffected.Should().BeApproximately(2.5, 1e-9);
        counties[1].OutageCount.Should().BeNull();
    }

    [Fact]
    public void ParseCounties_returns_empty_for_malformed_or_empty_input()
    {
        OutageJsonParser.ParseCounties("not json").Should().BeEmpty();
        OutageJsonParser.ParseCounties("").Should().BeEmpty();
        OutageJsonParser.ParseCounties("""{"data":[]}""").Should().BeEmpty();
    }

    [Fact]
    public void ParseOutages_reads_flat_lat_lon_fields()
    {
        const string Json = """
        {
          "data": [
            {
              "sourceEventNumber": "EVT-1",
              "deviceLatitudeLocation": 35.78,
              "deviceLongitudeLocation": -78.64,
              "customersAffected": 130,
              "cause": "Tree/Vegetation",
              "crewStatus": "Crew assigned",
              "outageStartTime": "2026-09-05T14:30:00Z",
              "estimatedTimeOfRestoration": "2026-09-05T18:00:00Z",
              "countyName": "Wake",
              "state": "NC"
            }
          ]
        }
        """;

        var outages = OutageJsonParser.ParseOutages(Json);

        outages.Should().ContainSingle();
        var outage = outages[0];
        outage.Id.Should().Be("EVT-1");
        outage.Latitude.Should().Be(35.78);
        outage.Longitude.Should().Be(-78.64);
        outage.CustomersAffected.Should().Be(130);
        outage.Cause.Should().Be("Tree/Vegetation");
        outage.Status.Should().Be("Crew assigned");
        outage.StartedAt.Should().Be(new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero));
        outage.EstimatedRestorationAt.Should().Be(new DateTimeOffset(2026, 9, 5, 18, 0, 0, TimeSpan.Zero));
        outage.County.Should().Be("Wake");
    }

    [Fact]
    public void ParseOutages_reads_geojson_features_with_lon_lat_coordinate_order()
    {
        const string Json = """
        {
          "features": [
            {
              "geometry": { "type": "Point", "coordinates": [-78.64, 35.78] },
              "properties": { "id": "EVT-2", "customersAffected": 7, "outageCause": "Equipment" }
            }
          ]
        }
        """;

        var outages = OutageJsonParser.ParseOutages(Json);

        outages.Should().ContainSingle();
        outages[0].Latitude.Should().Be(35.78);
        outages[0].Longitude.Should().Be(-78.64);
        outages[0].Id.Should().Be("EVT-2");
        outages[0].CustomersAffected.Should().Be(7);
        outages[0].Cause.Should().Be("Equipment");
    }

    [Fact]
    public void ParseOutages_accepts_a_bare_array_and_alternate_field_spellings()
    {
        const string Json = """
        [
          { "outageNumber": "X", "latitude": 1.5, "longitude": 2.5, "custAffected": 3, "status": "Assessing" }
        ]
        """;

        var outages = OutageJsonParser.ParseOutages(Json);

        outages.Should().ContainSingle();
        outages[0].Id.Should().Be("X");
        outages[0].Latitude.Should().Be(1.5);
        outages[0].CustomersAffected.Should().Be(3);
        outages[0].Status.Should().Be("Assessing");
    }

    [Fact]
    public void ParseOutages_reads_epoch_timestamps_in_milliseconds_and_seconds()
    {
        const string Json = """
        { "data": [ { "id": "E", "outageStartTime": 1757082600000, "etr": 1757095200 } ] }
        """;

        var outages = OutageJsonParser.ParseOutages(Json);

        outages[0].StartedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1757082600000));
        outages[0].EstimatedRestorationAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1757095200));
    }

    [Fact]
    public void ParseOutages_degrades_missing_fields_to_null_rather_than_failing()
    {
        var outages = OutageJsonParser.ParseOutages("""{"data":[{"somethingElse":1}]}""");

        outages.Should().ContainSingle();
        outages[0].Id.Should().BeNull();
        outages[0].Latitude.Should().BeNull();
        outages[0].CustomersAffected.Should().BeNull();
    }
}
