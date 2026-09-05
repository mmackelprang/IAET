using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy.Abstractions;

/// <summary>
/// Reads Duke Energy's public outage map: county rollups and individual outage events.
/// </summary>
public interface IOutageMapClient
{
    /// <summary>Gets the per-county outage rollup for a jurisdiction.</summary>
    /// <param name="jurisdiction">Operating-company code; defaults to the configured jurisdiction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One summary per county.</returns>
    Task<IReadOnlyList<CountyOutageSummary>> GetCountiesAsync(
        string? jurisdiction = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets every individual outage event for a jurisdiction.</summary>
    /// <param name="jurisdiction">Operating-company code; defaults to the configured jurisdiction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All outage events the API reports.</returns>
    Task<IReadOnlyList<OutageEvent>> GetOutagesAsync(
        string? jurisdiction = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the outages within a radius of a point, nearest first.</summary>
    /// <param name="latitude">Centre latitude, in degrees.</param>
    /// <param name="longitude">Centre longitude, in degrees.</param>
    /// <param name="radiusMiles">Search radius, in miles.</param>
    /// <param name="jurisdiction">Operating-company code; defaults to the configured jurisdiction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A report covering the requested radius.</returns>
    Task<NeighborhoodOutageReport> GetNeighborhoodAsync(
        double latitude,
        double longitude,
        double radiusMiles,
        string? jurisdiction = null,
        CancellationToken cancellationToken = default);
}
