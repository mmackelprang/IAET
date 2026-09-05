using System.Collections.Concurrent;
using System.Globalization;
using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy;

/// <summary>
/// Reads Duke Energy's public outage map over HTTP.
/// </summary>
/// <remarks>
/// Responses are cached for <see cref="DukeEnergyOptions.OutageCacheDuration"/>. Duke publishes
/// updates roughly every 15 minutes, so the cache exists to keep a polling REST front end from
/// generating load that cannot produce new data.
/// </remarks>
public sealed class OutageMapClient : IOutageMapClient
{
    private readonly HttpClient                       _httpClient;
    private readonly DukeEnergyOptions                _options;
    private readonly IDukeEnergyCredentialProvider    _credentials;
    private readonly TimeProvider                     _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="OutageMapClient"/> class.</summary>
    /// <param name="httpClient">Client used for outage-map requests.</param>
    /// <param name="options">Client configuration.</param>
    /// <param name="credentials">Supplies the Basic authorization header.</param>
    /// <param name="timeProvider">Clock used for caching; defaults to the system clock.</param>
    public OutageMapClient(
        HttpClient httpClient,
        DukeEnergyOptions options,
        IDukeEnergyCredentialProvider credentials,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credentials);

        _httpClient   = httpClient;
        _options      = options;
        _credentials  = credentials;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CountyOutageSummary>> GetCountiesAsync(
        string? jurisdiction = null,
        CancellationToken cancellationToken = default)
    {
        var code = Resolve(jurisdiction);
        var body = await GetCachedAsync(_options.CountiesPath, code, cancellationToken).ConfigureAwait(false);

        return OutageJsonParser.ParseCounties(body);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutageEvent>> GetOutagesAsync(
        string? jurisdiction = null,
        CancellationToken cancellationToken = default)
    {
        var code = Resolve(jurisdiction);
        var body = await GetCachedAsync(_options.OutagesPath, code, cancellationToken).ConfigureAwait(false);

        return OutageJsonParser.ParseOutages(body);
    }

    /// <inheritdoc />
    public async Task<NeighborhoodOutageReport> GetNeighborhoodAsync(
        double latitude,
        double longitude,
        double radiusMiles,
        string? jurisdiction = null,
        CancellationToken cancellationToken = default)
    {
        if (radiusMiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMiles), radiusMiles, "Radius must be greater than zero.");
        }

        var code    = Resolve(jurisdiction);
        var outages = await GetOutagesAsync(code, cancellationToken).ConfigureAwait(false);

        var nearby = outages
            .Where(o => o.Latitude is not null && o.Longitude is not null)
            .Select(o => o.WithDistance(
                GeoMath.DistanceMiles(latitude, longitude, o.Latitude!.Value, o.Longitude!.Value)))
            .Where(o => o.DistanceMiles <= radiusMiles)
            .OrderBy(o => o.DistanceMiles)
            .ToList();

        return new NeighborhoodOutageReport(
            new GeoPoint(latitude, longitude),
            radiusMiles,
            code,
            _timeProvider.GetUtcNow(),
            nearby);
    }

    private string Resolve(string? jurisdiction)
        => string.IsNullOrWhiteSpace(jurisdiction) ? _options.Jurisdiction : jurisdiction.Trim();

    private async Task<string> GetCachedAsync(string path, string jurisdiction, CancellationToken cancellationToken)
    {
        var cacheKey = string.Create(CultureInfo.InvariantCulture, $"{path}?{jurisdiction}");
        var now      = _timeProvider.GetUtcNow();

        if (_cache.TryGetValue(cacheKey, out var entry) && now < entry.ExpiresAt)
        {
            return entry.Body;
        }

        var body = await FetchAsync(path, jurisdiction, cancellationToken).ConfigureAwait(false);

        _cache[cacheKey] = new CacheEntry(body, now + _options.OutageCacheDuration);
        return body;
    }

    private async Task<string> FetchAsync(string path, string jurisdiction, CancellationToken cancellationToken)
    {
        var uri = new Uri(
            _options.OutageMapBaseUri,
            string.Create(CultureInfo.InvariantCulture, $"{path}?jurisdiction={Uri.EscapeDataString(jurisdiction)}"));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = await _credentials.GetAuthorizationAsync(cancellationToken).ConfigureAwait(false);
        ApplyBrowserHeaders(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A rejected key usually means the map front end rotated its credentials; drop the cached
        // header and try once more before surfacing the failure.
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            _credentials.Invalidate();

            using var retry = new HttpRequestMessage(HttpMethod.Get, uri);
            retry.Headers.Authorization = await _credentials.GetAuthorizationAsync(cancellationToken).ConfigureAwait(false);
            ApplyBrowserHeaders(retry);

            using var retried = await _httpClient.SendAsync(retry, cancellationToken).ConfigureAwait(false);
            retried.EnsureSuccessStatusCode();

            return await retried.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ApplyBrowserHeaders(HttpRequestMessage request)
    {
        var origin = _options.Origin.GetLeftPart(UriPartial.Authority);

        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Referer", origin + "/");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
    }

    private sealed record CacheEntry(string Body, DateTimeOffset ExpiresAt);
}
