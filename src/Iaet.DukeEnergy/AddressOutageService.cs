using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy;

/// <summary>
/// Geocodes a street address and reports the outages around it.
/// </summary>
public sealed class AddressOutageService : IAddressOutageService
{
    internal const string ProximityCaveat =
        "Proximity result, not a per-meter status. Duke Energy plots outages at device and transformer "
      + "locations rather than at premises, so a nearby event does not prove this address is on the "
      + "affected circuit, and finding none does not prove it has power — a single-premise outage often "
      + "never reaches the public map. For an authoritative answer, use the account-scoped endpoints.";

    private readonly IAddressGeocoder  _geocoder;
    private readonly IOutageMapClient  _outageMap;
    private readonly DukeEnergyOptions _options;

    /// <summary>Initializes a new instance of the <see cref="AddressOutageService"/> class.</summary>
    /// <param name="geocoder">Resolves the address to a coordinate.</param>
    /// <param name="outageMap">Public outage-map reader.</param>
    /// <param name="options">Client configuration.</param>
    public AddressOutageService(
        IAddressGeocoder geocoder,
        IOutageMapClient outageMap,
        DukeEnergyOptions options)
    {
        ArgumentNullException.ThrowIfNull(geocoder);
        ArgumentNullException.ThrowIfNull(outageMap);
        ArgumentNullException.ThrowIfNull(options);

        _geocoder  = geocoder;
        _outageMap = outageMap;
        _options   = options;
    }

    /// <inheritdoc />
    public async Task<AddressOutageReport?> GetByAddressAsync(
        string address,
        double? radiusMiles = null,
        string? jurisdiction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var located = await _geocoder.GeocodeAsync(address, cancellationToken).ConfigureAwait(false);

        if (located is null)
        {
            return null;
        }

        var radius = radiusMiles ?? _options.Geocoder.DefaultRadiusMiles;

        var neighborhood = await _outageMap
            .GetNeighborhoodAsync(
                located.Point.Latitude,
                located.Point.Longitude,
                radius,
                jurisdiction,
                cancellationToken)
            .ConfigureAwait(false);

        return new AddressOutageReport(located, neighborhood, ProximityCaveat);
    }
}
