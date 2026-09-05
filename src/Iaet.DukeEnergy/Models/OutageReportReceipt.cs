namespace Iaet.DukeEnergy.Models;

/// <summary>
/// Duke Energy's acknowledgement of a submitted outage report.
/// </summary>
/// <param name="Accepted">Whether Duke Energy accepted the report.</param>
/// <param name="ConfirmationNumber">Confirmation or ticket number, when returned.</param>
/// <param name="Message">Any human-readable message returned alongside the acknowledgement.</param>
/// <param name="SubmittedAt">When the submission was sent.</param>
/// <param name="DryRun">Whether the request was validated but deliberately not sent.</param>
/// <param name="Fields">Every field extracted from the response by the endpoint profile.</param>
public sealed record OutageReportReceipt(
    bool Accepted,
    string? ConfirmationNumber,
    string? Message,
    DateTimeOffset SubmittedAt,
    bool DryRun,
    IReadOnlyDictionary<string, string?> Fields);
