namespace Iaet.DukeEnergy.Models;

/// <summary>
/// Whether a specific account currently has an outage on file with Duke Energy.
/// </summary>
/// <param name="AccountNumber">The account queried.</param>
/// <param name="HasActiveOutage">Whether Duke Energy has an active outage recorded for the account.</param>
/// <param name="OutageId">Duke Energy's identifier for that outage, when reported.</param>
/// <param name="Status">Restoration status text, when reported.</param>
/// <param name="Cause">Reported cause, when known.</param>
/// <param name="ServiceAddress">The service address on the account, when reported.</param>
/// <param name="ReportedAt">When the outage was first recorded.</param>
/// <param name="EstimatedRestorationAt">Current estimated time of restoration.</param>
/// <param name="Fields">Every field extracted from the response by the endpoint profile.</param>
public sealed record AccountOutageStatus(
    string AccountNumber,
    bool HasActiveOutage,
    string? OutageId,
    string? Status,
    string? Cause,
    string? ServiceAddress,
    DateTimeOffset? ReportedAt,
    DateTimeOffset? EstimatedRestorationAt,
    IReadOnlyDictionary<string, string?> Fields);
