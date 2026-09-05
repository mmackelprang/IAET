using System.Net.Http.Headers;
using Iaet.DukeEnergy.Abstractions;

namespace Iaet.DukeEnergy.Tests;

/// <summary>
/// A hand-written stand-in rather than a mock: the interface returns a <see cref="ValueTask{T}"/>,
/// which cannot be handed to a mocking framework without misusing it.
/// </summary>
internal sealed class FakeCredentialProvider : IDukeEnergyCredentialProvider
{
    internal int InvalidateCount { get; private set; }

    internal int FetchCount { get; private set; }

    public ValueTask<AuthenticationHeaderValue> GetAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        FetchCount++;
        return ValueTask.FromResult(new AuthenticationHeaderValue("Basic", "dGVzdA=="));
    }

    public void Invalidate() => InvalidateCount++;
}
