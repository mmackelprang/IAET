namespace Iaet.DukeEnergy.Models;

/// <summary>
/// Combined answer to "is my power out, and is it just me?".
/// </summary>
/// <param name="Label">Friendly label for the service location.</param>
/// <param name="ServiceAddress">
/// The service address Duke Energy has on the account, once it has been resolved. This is the
/// authoritative address for the premises, as opposed to a geocoded guess.
/// </param>
/// <param name="GeneratedAt">When this status was produced.</param>
/// <param name="Account">
/// Account-scoped outage status, or <see langword="null"/> when the account flow is not configured.
/// </param>
/// <param name="Neighborhood">
/// Nearby outages, or <see langword="null"/> when no home coordinates are configured.
/// </param>
/// <param name="County">County-level rollup, when a home county is configured.</param>
/// <param name="Notes">Explanations for anything that could not be answered.</param>
public sealed record HomeOutageStatus(
    string? Label,
    string? ServiceAddress,
    DateTimeOffset GeneratedAt,
    AccountOutageStatus? Account,
    NeighborhoodOutageReport? Neighborhood,
    CountyOutageSummary? County,
    IReadOnlyList<string> Notes)
{
    /// <summary>
    /// Whether anything indicates an outage at or around this location: an outage on the account,
    /// or at least one outage inside the neighborhood radius.
    /// </summary>
    public bool OutageIndicated =>
        (Account?.HasActiveOutage ?? false) || (Neighborhood?.OutageCount ?? 0) > 0;
}
