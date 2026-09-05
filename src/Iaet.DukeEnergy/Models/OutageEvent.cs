namespace Iaet.DukeEnergy.Models;

/// <summary>
/// A single outage event from the outage-map API, normalized into stable field names.
/// </summary>
/// <param name="Id">Duke Energy's identifier for the event, when present.</param>
/// <param name="Latitude">Latitude of the affected device, in degrees.</param>
/// <param name="Longitude">Longitude of the affected device, in degrees.</param>
/// <param name="CustomersAffected">Customer accounts affected by this event.</param>
/// <param name="Cause">Reported cause, for example <c>"Tree/Vegetation"</c>.</param>
/// <param name="Status">Crew or restoration status, for example <c>"Crew assigned"</c>.</param>
/// <param name="StartedAt">When the outage began.</param>
/// <param name="EstimatedRestorationAt">Current estimated time of restoration.</param>
/// <param name="County">County the event was reported in.</param>
/// <param name="State">Two-letter state code.</param>
/// <param name="DistanceMiles">
/// Distance from the point a neighborhood query was centred on. <see langword="null"/> outside a
/// proximity query.
/// </param>
public sealed record OutageEvent(
    string? Id,
    double? Latitude,
    double? Longitude,
    int? CustomersAffected,
    string? Cause,
    string? Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EstimatedRestorationAt,
    string? County,
    string? State,
    double? DistanceMiles = null)
{
    /// <summary>Returns a copy of this event tagged with its distance from a query centre.</summary>
    /// <param name="distanceMiles">Distance in miles.</param>
    public OutageEvent WithDistance(double distanceMiles) => this with { DistanceMiles = distanceMiles };
}
