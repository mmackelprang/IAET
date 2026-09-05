namespace Iaet.DukeEnergy.Models;

/// <summary>
/// Every known outage within a radius of a point, ordered nearest first.
/// </summary>
/// <param name="Center">The point the search was centred on.</param>
/// <param name="RadiusMiles">The search radius in miles.</param>
/// <param name="Jurisdiction">The operating-company code that was queried.</param>
/// <param name="GeneratedAt">When this report was produced.</param>
/// <param name="Outages">Matching outages, nearest first.</param>
public sealed record NeighborhoodOutageReport(
    GeoPoint Center,
    double RadiusMiles,
    string Jurisdiction,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<OutageEvent> Outages)
{
    /// <summary>Number of outage events inside the radius.</summary>
    public int OutageCount => Outages.Count;

    /// <summary>Total customers affected across all matching events.</summary>
    public int CustomersAffected => Outages.Sum(o => o.CustomersAffected ?? 0);

    /// <summary>Distance to the closest matching outage, in miles.</summary>
    public double? NearestOutageMiles => Outages.Count == 0 ? null : Outages[0].DistanceMiles;
}
