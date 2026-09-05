using System.Net.Http.Headers;

namespace Iaet.DukeEnergy.Abstractions;

/// <summary>
/// Supplies the Basic authorization header the outage-map API expects.
/// </summary>
public interface IDukeEnergyCredentialProvider
{
    /// <summary>Gets a cached or freshly fetched authorization header.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The header to attach to outage-map requests.</returns>
    ValueTask<AuthenticationHeaderValue> GetAuthorizationAsync(CancellationToken cancellationToken = default);

    /// <summary>Discards cached credentials so the next call re-fetches them.</summary>
    void Invalidate();
}
