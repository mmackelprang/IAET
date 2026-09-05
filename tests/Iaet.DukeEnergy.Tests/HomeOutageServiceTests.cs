using FluentAssertions;
using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;
using NSubstitute;

namespace Iaet.DukeEnergy.Tests;

public class HomeOutageServiceTests
{
    private static NeighborhoodOutageReport EmptyReport(double radius = 1.0) =>
        new(new GeoPoint(35.78, -78.64), radius, Jurisdictions.Carolinas, DateTimeOffset.UnixEpoch, []);

    private static DukeEnergyOptions HomeAt(double? lat = 35.78, double? lon = -78.64)
    {
        var options = new DukeEnergyOptions();
        options.Home.Label     = "Home";
        options.Home.Latitude  = lat;
        options.Home.Longitude = lon;
        return options;
    }

    [Fact]
    public async Task Reports_the_neighborhood_and_notes_the_unconfigured_account_flow()
    {
        var map = Substitute.For<IOutageMapClient>();
        map.GetNeighborhoodAsync(35.78, -78.64, 1.0, Arg.Any<string?>(), Arg.Any<CancellationToken>())
           .Returns(EmptyReport() with
           {
               Outages = [new OutageEvent("E", 35.78, -78.64, 12, null, null, null, null, null, null, 0.1)],
           });

        var report = Substitute.For<IOutageReportClient>();
        report.IsConfigured.Returns(false);
        report.ConfigurationProblem.Returns("profile missing");

        var service = new HomeOutageService(map, report, HomeAt());

        var status = await service.GetHomeStatusAsync();

        status.Label.Should().Be("Home");
        status.Neighborhood!.OutageCount.Should().Be(1);
        status.Account.Should().BeNull();
        status.Notes.Should().Contain("profile missing");
        status.OutageIndicated.Should().BeTrue();
    }

    [Fact]
    public async Task Notes_missing_home_coordinates_instead_of_failing()
    {
        var map    = Substitute.For<IOutageMapClient>();
        var report = Substitute.For<IOutageReportClient>();
        report.IsConfigured.Returns(false);

        var status = await new HomeOutageService(map, report, HomeAt(lat: null, lon: null)).GetHomeStatusAsync();

        status.Neighborhood.Should().BeNull();
        status.Notes.Should().Contain(n => n.Contains("Latitude", StringComparison.Ordinal));
        status.OutageIndicated.Should().BeFalse();
    }

    [Fact]
    public async Task Resolves_the_account_from_the_configured_phone_number()
    {
        var map = Substitute.For<IOutageMapClient>();
        map.GetNeighborhoodAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
           .Returns(EmptyReport());

        var report = Substitute.For<IOutageReportClient>();
        report.IsConfigured.Returns(true);
        report.LookupAccountByPhoneAsync("9195550100", Arg.Any<CancellationToken>())
              .Returns(new AccountLookupResult(true, "ACC-1", "1 Main St", new Dictionary<string, string?>()));
        report.GetExistingOutageAsync("ACC-1", Arg.Any<CancellationToken>())
              .Returns(new AccountOutageStatus("ACC-1", true, "OUT-3", "Assessing", null, null, null,
                  new Dictionary<string, string?>()));

        var options = HomeAt();
        options.Home.PhoneNumber = "9195550100";

        var status = await new HomeOutageService(map, report, options).GetHomeStatusAsync();

        status.Account!.OutageId.Should().Be("OUT-3");
        status.OutageIndicated.Should().BeTrue();
    }

    [Fact]
    public async Task An_outage_map_failure_becomes_a_note_rather_than_an_exception()
    {
        var map = Substitute.For<IOutageMapClient>();
        map.GetNeighborhoodAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
           .Returns<NeighborhoodOutageReport>(_ => throw new HttpRequestException("upstream down"));

        var report = Substitute.For<IOutageReportClient>();
        report.IsConfigured.Returns(false);

        var status = await new HomeOutageService(map, report, HomeAt()).GetHomeStatusAsync();

        status.Neighborhood.Should().BeNull();
        status.Notes.Should().Contain(n => n.Contains("upstream down", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Includes_the_county_rollup_when_a_home_county_is_configured()
    {
        var map = Substitute.For<IOutageMapClient>();
        map.GetNeighborhoodAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
           .Returns(EmptyReport());
        map.GetCountiesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
           .Returns<IReadOnlyList<CountyOutageSummary>>(_ =>
           [
               new CountyOutageSummary("NC", "Wake", 100, 5, 2),
               new CountyOutageSummary("NC", "Durham", 50, 0, 0),
           ]);

        var report = Substitute.For<IOutageReportClient>();
        report.IsConfigured.Returns(false);

        var options = HomeAt();
        options.Home.County = "wake";
        options.Home.State  = "NC";

        var status = await new HomeOutageService(map, report, options).GetHomeStatusAsync();

        status.County!.CountyName.Should().Be("Wake");
        status.County.CustomersAffected.Should().Be(5);
    }
}
