using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy.Abstractions;

/// <summary>
/// Drives the account-scoped outage-report flow: resolve an account, read its outage status, and
/// file a new report.
/// </summary>
/// <remarks>
/// Every operation is defined by a captured endpoint profile rather than by hard-coded URLs,
/// because Duke Energy does not publish this API.
/// </remarks>
public interface IOutageReportClient
{
    /// <summary>
    /// Whether an endpoint profile is loaded and the flow has been enabled in configuration.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Explains why <see cref="IsConfigured"/> is <see langword="false"/>.</summary>
    string? ConfigurationProblem { get; }

    /// <summary>Resolves a Duke Energy account from a phone number.</summary>
    /// <param name="phoneNumber">Phone number on the account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matched account, if any.</returns>
    Task<AccountLookupResult> LookupAccountByPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the outage Duke Energy currently has on file for an account.</summary>
    /// <param name="accountNumber">Account number to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account's outage status.</returns>
    Task<AccountOutageStatus> GetExistingOutageAsync(
        string accountNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Files a new outage report.</summary>
    /// <param name="request">The report to submit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Duke Energy's acknowledgement.</returns>
    Task<OutageReportReceipt> SubmitReportAsync(
        OutageReportRequest request,
        CancellationToken cancellationToken = default);
}
