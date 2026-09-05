namespace Iaet.DukeEnergy.Models;

/// <summary>
/// Outage rollup for a single county, as published by the outage-map API.
/// </summary>
/// <param name="State">Two-letter state code.</param>
/// <param name="CountyName">County name.</param>
/// <param name="CustomersServed">Total customer accounts Duke Energy serves in the county.</param>
/// <param name="CustomersAffected">Customer accounts currently without power.</param>
/// <param name="OutageCount">Number of distinct outage events, when the API reports it.</param>
public sealed record CountyOutageSummary(
    string? State,
    string CountyName,
    int CustomersServed,
    int CustomersAffected,
    int? OutageCount)
{
    /// <summary>
    /// Percentage of served customers currently affected, or <see langword="null"/> when the
    /// served-customer count is unknown.
    /// </summary>
    public double? PercentAffected =>
        CustomersServed > 0 ? CustomersAffected * 100.0 / CustomersServed : null;
}
