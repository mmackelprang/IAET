using System.Text.Json;
using Iaet.Core.Abstractions;
using Microsoft.Playwright;

namespace Iaet.Capture.Cdp;

/// <summary>
/// Wraps Playwright's <see cref="ICDPSession"/> to implement <see cref="ICdpSession"/>.
/// </summary>
public sealed class PlaywrightCdpSession : ICdpSession, IAsyncDisposable
{
    private readonly ICDPSession _cdp;
    private readonly HashSet<string> _subscribedDomains = [];

    private PlaywrightCdpSession(ICDPSession cdp)
    {
        _cdp = cdp;
    }

    /// <summary>
    /// Creates a new <see cref="PlaywrightCdpSession"/> for the given page.
    /// </summary>
    public static async Task<PlaywrightCdpSession> CreateAsync(IPage page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ct.ThrowIfCancellationRequested();
        var cdp = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        return new PlaywrightCdpSession(cdp);
    }

    /// <inheritdoc/>
    public async Task SubscribeToDomainAsync(string domain, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ct.ThrowIfCancellationRequested();

        if (!_subscribedDomains.Add(domain))
        {
            return;
        }

        await _cdp.SendAsync($"{domain}.enable").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UnsubscribeFromDomainAsync(string domain, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ct.ThrowIfCancellationRequested();

        if (!_subscribedDomains.Remove(domain))
        {
            return;
        }

        await _cdp.SendAsync($"{domain}.disable").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IDisposable OnEvent(string eventName, Action<JsonElement> handler)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        void OnEventFired(object? sender, JsonElement? e)
        {
            if (e.HasValue)
            {
                handler(e.Value);
            }
        }

        _cdp.Event(eventName).OnEvent += OnEventFired;

        // Playwright does not support CDP event unsubscription; the disposable is a no-op.
        return NullDisposable.Instance;
    }

    /// <inheritdoc/>
    public async Task<JsonElement> SendCommandAsync(
        string method,
        object? parameters = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ct.ThrowIfCancellationRequested();

        Dictionary<string, object>? args = null;
        if (parameters is not null)
        {
            var json = JsonSerializer.SerializeToElement(parameters);
            args = [];
            foreach (var property in json.EnumerateObject())
            {
                args[property.Name] = property.Value;
            }
        }

        var result = await _cdp.SendAsync(method, args).ConfigureAwait(false);
        return result ?? default;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        var domains = _subscribedDomains.ToArray();
        foreach (var domain in domains)
        {
            try
            {
                await _cdp.SendAsync($"{domain}.disable").ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Intentionally swallowing: browser may already be closing during dispose
            catch
            {
                // Browser may already be closing; best-effort cleanup.
            }
#pragma warning restore CA1031

            _subscribedDomains.Remove(domain);
        }

        await _cdp.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// A no-op <see cref="IDisposable"/> returned by <see cref="OnEvent"/> because
    /// Playwright does not support CDP event unsubscription.
    /// </summary>
    private sealed class NullDisposable : IDisposable
    {
        internal static readonly NullDisposable Instance = new();

        private NullDisposable() { }

        public void Dispose() { }
    }
}
