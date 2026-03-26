using System.Text.Json;

namespace Iaet.Core.Abstractions;

/// <summary>
/// Abstraction over Chrome DevTools Protocol session.
/// Provides domain management, event subscription, and command execution.
/// </summary>
public interface ICdpSession
{
    Task SubscribeToDomainAsync(string domain, CancellationToken ct = default);
    Task UnsubscribeFromDomainAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to a CDP event. Returns a disposable that unsubscribes when disposed.
    /// </summary>
    IDisposable OnEvent(string eventName, Action<JsonElement> handler);

    /// <summary>
    /// Send a CDP command and return the result.
    /// </summary>
    Task<JsonElement> SendCommandAsync(string method, object? parameters = null, CancellationToken ct = default);
}
