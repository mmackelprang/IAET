using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy.Abstractions;

/// <summary>
/// Answers "is my power out, and is it just me?" by combining the account-scoped outage status
/// with nearby and county-level outage data.
/// </summary>
public interface IHomeOutageService
{
    /// <summary>Builds the combined status for the configured home location.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined status.</returns>
    Task<HomeOutageStatus> GetHomeStatusAsync(CancellationToken cancellationToken = default);
}
