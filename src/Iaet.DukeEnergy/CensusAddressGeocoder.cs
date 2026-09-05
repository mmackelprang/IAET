using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy;

/// <summary>
/// Resolves US street addresses using the US Census Bureau geocoder.
/// </summary>
/// <remarks>
/// Chosen because it is free, needs no API key, and covers exactly the US footprint Duke Energy
/// serves. Results are cached because addresses do not move, which also keeps a polling caller from
/// hammering a public service.
/// </remarks>
public sealed class CensusAddressGeocoder : IAddressGeocoder
{
    private readonly HttpClient        _httpClient;
    private readonly DukeEnergyOptions _options;
    private readonly TimeProvider      _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="CensusAddressGeocoder"/> class.</summary>
    /// <param name="httpClient">Client used for geocoder requests.</param>
    /// <param name="options">Client configuration.</param>
    /// <param name="timeProvider">Clock used for caching; defaults to the system clock.</param>
    public CensusAddressGeocoder(
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
    public async Task<GeocodedAddress?> GeocodeAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var key = address.Trim();
        var now = _timeProvider.GetUtcNow();

        if (_cache.TryGetValue(key, out var cached) && now < cached.ExpiresAt)
        {
            return cached.Result;
        }

        var result = await FetchAsync(key, cancellationToken).ConfigureAwait(false);

        _cache[key] = new CacheEntry(result, now + _options.Geocoder.CacheDuration);
        return result;
    }

    private async Task<GeocodedAddress?> FetchAsync(string address, CancellationToken cancellationToken)
    {
        var uri = new Uri(
            _options.Geocoder.BaseUri,
            string.Create(
                CultureInfo.InvariantCulture,
                $"?address={Uri.EscapeDataString(address)}&benchmark={Uri.EscapeDataString(_options.Geocoder.Benchmark)}&format=json"));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return Parse(address, body);
    }

    /// <summary>
    /// Reads the first address match. The Census geocoder reports coordinates as <c>x</c> for
    /// longitude and <c>y</c> for latitude.
    /// </summary>
    /// <param name="address">The address as supplied by the caller.</param>
    /// <param name="json">The raw geocoder response.</param>
    /// <returns>The first match, or <see langword="null"/> when nothing matched.</returns>
    internal static GeocodedAddress? Parse(string address, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (!JsonPathLite.TrySelect(document.RootElement, "result.addressMatches[0]", out var match))
            {
                return null;
            }

            var longitude = JsonPathLite.SelectString(match, "coordinates.x");
            var latitude  = JsonPathLite.SelectString(match, "coordinates.y");

            if (!double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                || !double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            {
                return null;
            }

            return new GeocodedAddress(
                address,
                JsonPathLite.SelectString(match, "matchedAddress"),
                new GeoPoint(lat, lon),
                "census");
        }
    }

    private sealed record CacheEntry(GeocodedAddress? Result, DateTimeOffset ExpiresAt);
}
