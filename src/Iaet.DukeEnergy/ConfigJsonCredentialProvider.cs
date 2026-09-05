using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Iaet.DukeEnergy.Abstractions;

namespace Iaet.DukeEnergy;

/// <summary>
/// Reads the outage-map API consumer key and secret from the front-end configuration document and
/// turns them into a Basic authorization header.
/// </summary>
/// <remarks>
/// These are public client credentials shipped to every browser that loads the outage map; they
/// identify the map application, not a customer. They are cached in memory only and never written
/// to disk or logs.
/// </remarks>
public sealed class ConfigJsonCredentialProvider : IDukeEnergyCredentialProvider, IDisposable
{
    private static readonly string[] KeyNames    = ["consumer_key_emp", "consumerKeyEmp", "consumer_key", "consumerKey"];
    private static readonly string[] SecretNames = ["consumer_secret_emp", "consumerSecretEmp", "consumer_secret", "consumerSecret"];

    private readonly HttpClient          _httpClient;
    private readonly DukeEnergyOptions   _options;
    private readonly TimeProvider        _timeProvider;
    private readonly SemaphoreSlim       _gate = new(1, 1);

    private AuthenticationHeaderValue? _cached;
    private DateTimeOffset             _cachedUntil;
    private bool                       _disposed;

    /// <summary>Initializes a new instance of the <see cref="ConfigJsonCredentialProvider"/> class.</summary>
    /// <param name="httpClient">Client used to fetch the configuration document.</param>
    /// <param name="options">Client configuration.</param>
    /// <param name="timeProvider">Clock used for cache expiry; defaults to the system clock.</param>
    public ConfigJsonCredentialProvider(
        HttpClient httpClient,
        DukeEnergyOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient   = httpClient;
        _options      = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<AuthenticationHeaderValue> GetAuthorizationAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        if (_cached is not null && now < _cachedUntil)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_cached is not null && now < _cachedUntil)
            {
                return _cached;
            }

            var header = await FetchAsync(cancellationToken).ConfigureAwait(false);

            _cached      = header;
            _cachedUntil = now + _options.CredentialCacheDuration;

            return header;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        _cached      = null;
        _cachedUntil = default;
    }

    private async Task<AuthenticationHeaderValue> FetchAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.ConfigUri);
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(body);

        var key    = FindFirst(document.RootElement, KeyNames);
        var secret = FindFirst(document.RootElement, SecretNames);

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"No outage-map consumer key/secret found in {_options.ConfigUri}. The configuration document layout has changed; re-run an IAET capture of the outage map to find the new field names."));
        }

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}"));
        return new AuthenticationHeaderValue("Basic", token);
    }

    /// <summary>
    /// Finds the first matching key, searching the document root and then one level of nesting —
    /// the configuration document has grouped its credentials under a child object before.
    /// </summary>
    private static string? FindFirst(JsonElement root, string[] names)
    {
        foreach (var name in names)
        {
            if (JsonPathLite.TryGetPropertyIgnoreCase(root, name, out var value))
            {
                var text = JsonPathLite.AsString(value);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var name in names)
            {
                if (JsonPathLite.TryGetPropertyIgnoreCase(property.Value, name, out var value))
                {
                    var text = JsonPathLite.AsString(value);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }
}
