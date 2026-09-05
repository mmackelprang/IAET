using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy.Abstractions;

/// <summary>
/// Resolves a street address to a coordinate so it can be matched against the outage map.
/// </summary>
public interface IAddressGeocoder
{
    /// <summary>Resolves a street address.</summary>
    /// <param name="address">A one-line street address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The match, or <see langword="null"/> when the address could not be resolved.</returns>
    Task<GeocodedAddress?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}
