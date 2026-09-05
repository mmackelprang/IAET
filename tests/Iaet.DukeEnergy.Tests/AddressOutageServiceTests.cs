using FluentAssertions;
using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;
using NSubstitute;

namespace Iaet.DukeEnergy.Tests;

public class AddressOutageServiceTests
{
    private static readonly GeocodedAddress Located =
        new("123 Main St", "123 MAIN ST, RALEIGH, NC, 27601", new GeoPoint(35.7796, -78.6382), "census");

    private static IAddressGeocoder Geocoder(GeocodedAddress? result)
    {
        var geocoder = Substitute.For<IAddressGeocoder>();
        geocoder.GeocodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        return geocoder;
    }

    private static IOutageMapClient MapWith(params OutageEvent[] outages)
    {
        var map = Substitute.For<IOutageMapClient>();
        map.GetNeighborhoodAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
           .Returns(call => new NeighborhoodOutageReport(
               new GeoPoint(call.ArgAt<double>(0), call.ArgAt<double>(1)),
               call.ArgAt<double>(2),
               Jurisdictions.Carolinas,
               DateTimeOffset.UnixEpoch,
               outages));
        return map;
    }

    [Fact]
    public async Task GetByAddressAsync_geocodes_then_searches_around_the_resolved_point()
    {
        var map = MapWith(new OutageEvent("E", 35.78, -78.64, 12, null, null, null, null, null, null, 0.12));

        var report = await new AddressOutageService(Geocoder(Located), map, new DukeEnergyOptions())
            .GetByAddressAsync("123 Main St");

        report.Should().NotBeNull();
        report!.Address.MatchedAddress.Should().Be("123 MAIN ST, RALEIGH, NC, 27601");
        report.OutageNearby.Should().BeTrue();
        report.NearestOutageMiles.Should().Be(0.12);

        await map.Received(1).GetNeighborhoodAsync(
            35.7796, -78.6382, 0.25, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByAddressAsync_uses_the_tighter_address_radius_by_default()
    {
        var options = new DukeEnergyOptions();

        options.Geocoder.DefaultRadiusMiles.Should().Be(0.25);
        options.Home.RadiusMiles.Should().Be(1.0, "an address query is about one premises, not a neighborhood");

        var map = MapWith();
        await new AddressOutageService(Geocoder(Located), map, options).GetByAddressAsync("123 Main St");

        await map.Received(1).GetNeighborhoodAsync(
            Arg.Any<double>(), Arg.Any<double>(), 0.25, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByAddressAsync_honours_an_explicit_radius_and_jurisdiction()
    {
        var map = MapWith();

        await new AddressOutageService(Geocoder(Located), map, new DukeEnergyOptions())
            .GetByAddressAsync("123 Main St", radiusMiles: 2.5, jurisdiction: Jurisdictions.Florida);

        await map.Received(1).GetNeighborhoodAsync(
            Arg.Any<double>(), Arg.Any<double>(), 2.5, Jurisdictions.Florida, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByAddressAsync_returns_null_when_the_address_cannot_be_located()
    {
        var map = MapWith();

        var report = await new AddressOutageService(Geocoder(null), map, new DukeEnergyOptions())
            .GetByAddressAsync("nowhere at all");

        report.Should().BeNull();
        await map.DidNotReceiveWithAnyArgs().GetNeighborhoodAsync(0, 0, 0);
    }

    [Fact]
    public async Task GetByAddressAsync_carries_the_proximity_caveat_in_the_payload()
    {
        var report = await new AddressOutageService(Geocoder(Located), MapWith(), new DukeEnergyOptions())
            .GetByAddressAsync("123 Main St");

        // The caveat travels with the answer so a consumer cannot read it as a per-meter status.
        report!.Caveat.Should().Contain("not a per-meter status");
        report.OutageNearby.Should().BeFalse();
    }

    [Fact]
    public async Task GetByAddressAsync_rejects_a_blank_address()
    {
        var service = new AddressOutageService(Geocoder(Located), MapWith(), new DukeEnergyOptions());

        var act = async () => await service.GetByAddressAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
