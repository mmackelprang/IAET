using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy.Abstractions;

/// <summary>
/// Answers what the public outage map can say about a street address.
/// </summary>
public interface IAddressOutageService
{
    /// <summary>Geocodes an address and reports the outages around it.</summary>
    /// <param name="address">A one-line street address.</param>
    /// <param name="radiusMiles">Search radius; defaults to the configured address radius.</param>
    /// <param name="jurisdiction">Operating-company code; defaults to the configured jurisdiction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The proximity report, or <see langword="null"/> when the address could not be geocoded.
    /// </returns>
    Task<AddressOutageReport?> GetByAddressAsync(
        string address,
        double? radiusMiles = null,
        string? jurisdiction = null,
        CancellationToken cancellationToken = default);
}
