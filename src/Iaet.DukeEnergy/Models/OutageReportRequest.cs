namespace Iaet.DukeEnergy.Models;

/// <summary>
/// A request to report a new outage at a service location.
/// </summary>
/// <param name="AccountNumber">The Duke Energy account to report against.</param>
/// <param name="PhoneNumber">Contact phone number on the account.</param>
/// <param name="Comments">Free-text detail, for example <c>"Loud bang, transformer smoking"</c>.</param>
/// <param name="ContactEmail">Optional address for restoration updates.</param>
public sealed record OutageReportRequest(
    string AccountNumber,
    string? PhoneNumber = null,
    string? Comments = null,
    string? ContactEmail = null);
