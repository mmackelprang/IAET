namespace Iaet.DukeEnergy.Models;

/// <summary>
/// What the public outage map can say about a street address.
/// </summary>
/// <remarks>
/// This is a proximity answer, not a per-meter one. Duke Energy plots outages at device and
/// transformer locations rather than at premises, so a nearby event does not prove this address is
/// on the affected circuit, and the absence of one does not prove the address has power — a
/// single-premise outage frequently never reaches the public map at all. <see cref="Caveat"/>
/// carries that warning so it travels with the payload instead of living only in documentation.
/// </remarks>
/// <param name="Address">The address as resolved by the geocoder.</param>
/// <param name="Neighborhood">Outages found within the search radius, nearest first.</param>
/// <param name="Caveat">Plain-language statement of what this answer can and cannot establish.</param>
public sealed record AddressOutageReport(
    GeocodedAddress Address,
    NeighborhoodOutageReport Neighborhood,
    string Caveat)
{
    /// <summary>Whether at least one outage was found within the search radius.</summary>
    public bool OutageNearby => Neighborhood.OutageCount > 0;

    /// <summary>Distance to the closest outage in miles, when one was found.</summary>
    public double? NearestOutageMiles => Neighborhood.NearestOutageMiles;
}
