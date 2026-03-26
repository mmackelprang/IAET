namespace Iaet.Core.Abstractions;

/// <summary>
/// Abstraction over Chrome DevTools Protocol session.
/// Full implementation provided by Iaet.Capture in Phase 2.
/// </summary>
public interface ICdpSession
{
    Task SubscribeToDomainAsync(string domain, CancellationToken ct = default);
    Task UnsubscribeFromDomainAsync(string domain, CancellationToken ct = default);
}
