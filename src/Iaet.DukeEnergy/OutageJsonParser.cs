using System.Globalization;
using System.Text.Json;
using Iaet.DukeEnergy.Models;

namespace Iaet.DukeEnergy;

/// <summary>
/// Normalizes outage-map payloads into <see cref="CountyOutageSummary"/> and
/// <see cref="OutageEvent"/>.
/// </summary>
/// <remarks>
/// Duke Energy publishes no schema for this API and has changed field names before, so the parser
/// accepts several spellings per field, unwraps the common envelopes (<c>data</c>, <c>results</c>,
/// GeoJSON <c>features</c>), and treats every field as optional. A renamed field degrades one
/// property to <see langword="null"/> instead of failing the whole response.
/// </remarks>
public static class OutageJsonParser
{
    private static readonly string[] EnvelopeKeys  = ["data", "results", "outages", "counties", "features", "items"];
    private static readonly string[] NestedObjects = ["properties", "attributes", "areaOfInterestSummary", "summary"];

    private static readonly string[] CountyNameKeys       = ["countyName", "county", "name", "areaName"];
    private static readonly string[] StateKeys            = ["state", "stateCode", "stateName"];
    private static readonly string[] CustomersServedKeys  = ["customersServed", "customerCount", "totalCustomers", "accounts"];
    private static readonly string[] CustomersAffectedKeys =
    [
        "customersAffected", "custAffected", "customersOut", "maxCustomersAffected", "totalCustomersAffected",
    ];
    private static readonly string[] OutageCountKeys      = ["outageCount", "numberOfOutages", "totalOutages", "outages"];

    private static readonly string[] IdKeys        = ["sourceEventNumber", "outageNumber", "eventId", "id", "outageId"];
    private static readonly string[] LatitudeKeys  = ["deviceLatitudeLocation", "latitude", "lat", "y"];
    private static readonly string[] LongitudeKeys = ["deviceLongitudeLocation", "longitude", "lng", "lon", "x"];
    private static readonly string[] CauseKeys     = ["cause", "outageCause", "causeDescription", "comments"];
    private static readonly string[] StatusKeys    = ["crewStatus", "outageStatus", "status", "restorationStatus"];
    private static readonly string[] StartedAtKeys = ["outageStartTime", "startTime", "beginTime", "reportedTime", "outageTime"];
    private static readonly string[] EtrKeys       =
    [
        "estimatedTimeOfRestoration", "estimatedRestorationTime", "etr", "estimatedRestoration",
    ];

    /// <summary>Parses a per-county outage rollup payload.</summary>
    /// <param name="json">The raw response body.</param>
    /// <returns>One summary per county entry found.</returns>
    public static IReadOnlyList<CountyOutageSummary> ParseCounties(string json)
    {
        var results = new List<CountyOutageSummary>();

        foreach (var item in EnumerateRecords(json))
        {
            var name = FindString(item, CountyNameKeys);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            results.Add(new CountyOutageSummary(
                FindString(item, StateKeys),
                name,
                FindInt(item, CustomersServedKeys) ?? 0,
                FindInt(item, CustomersAffectedKeys) ?? 0,
                FindInt(item, OutageCountKeys)));
        }

        return results;
    }

    /// <summary>Parses an individual outage event payload.</summary>
    /// <param name="json">The raw response body.</param>
    /// <returns>One event per entry found.</returns>
    public static IReadOnlyList<OutageEvent> ParseOutages(string json)
    {
        var results = new List<OutageEvent>();

        foreach (var item in EnumerateRecords(json))
        {
            var (latitude, longitude) = FindCoordinates(item);

            results.Add(new OutageEvent(
                FindString(item, IdKeys),
                latitude,
                longitude,
                FindInt(item, CustomersAffectedKeys),
                FindString(item, CauseKeys),
                FindString(item, StatusKeys),
                FindTimestamp(item, StartedAtKeys),
                FindTimestamp(item, EtrKeys),
                FindString(item, CountyNameKeys),
                FindString(item, StateKeys)));
        }

        return results;
    }

    /// <summary>
    /// Unwraps the response envelope and yields each record, keeping the original element so that
    /// nested objects such as GeoJSON geometry stay reachable.
    /// </summary>
    private static List<JsonElement> EnumerateRecords(string json)
    {
        var records = new List<JsonElement>();

        if (string.IsNullOrWhiteSpace(json))
        {
            return records;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return records;
        }

        using (document)
        {
            var root = document.RootElement;

            foreach (var key in EnvelopeKeys)
            {
                if (root.ValueKind == JsonValueKind.Array)
                {
                    break;
                }

                if (JsonPathLite.TryGetPropertyIgnoreCase(root, key, out var inner)
                    && inner.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    root = inner;
                }
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    // Clone: the JsonDocument is disposed before the caller reads these.
                    records.Add(element.Clone());
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                records.Add(root.Clone());
            }
        }

        return records;
    }

    /// <summary>
    /// Looks up a key on the record itself and then on each well-known nested object, so that both
    /// flat payloads and GeoJSON-style <c>properties</c> wrappers resolve.
    /// </summary>
    private static bool TryFind(JsonElement item, string[] keys, out JsonElement value)
    {
        foreach (var key in keys)
        {
            if (JsonPathLite.TryGetPropertyIgnoreCase(item, key, out value)
                && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                return true;
            }
        }

        foreach (var container in NestedObjects)
        {
            if (!JsonPathLite.TryGetPropertyIgnoreCase(item, container, out var nested))
            {
                continue;
            }

            foreach (var key in keys)
            {
                if (JsonPathLite.TryGetPropertyIgnoreCase(nested, key, out value)
                    && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? FindString(JsonElement item, string[] keys)
        => TryFind(item, keys, out var value) ? JsonPathLite.AsString(value) : null;

    private static int? FindInt(JsonElement item, string[] keys)
    {
        if (!TryFind(item, keys, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        // "outages" can be either a count or the array it counts.
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.GetArrayLength();
        }

        var text = JsonPathLite.AsString(value);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double? FindDouble(JsonElement item, string[] keys)
    {
        if (!TryFind(item, keys, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        var text = JsonPathLite.AsString(value);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Resolves a coordinate pair from either named latitude/longitude fields or a GeoJSON
    /// <c>geometry.coordinates</c> array, which is <c>[longitude, latitude]</c>.
    /// </summary>
    private static (double? Latitude, double? Longitude) FindCoordinates(JsonElement item)
    {
        var latitude  = FindDouble(item, LatitudeKeys);
        var longitude = FindDouble(item, LongitudeKeys);

        if (latitude is not null && longitude is not null)
        {
            return (latitude, longitude);
        }

        if (JsonPathLite.TrySelect(item, "geometry.coordinates", out var coordinates)
            && coordinates.ValueKind == JsonValueKind.Array
            && coordinates.GetArrayLength() >= 2
            && coordinates[0].ValueKind == JsonValueKind.Number
            && coordinates[1].ValueKind == JsonValueKind.Number)
        {
            return (coordinates[1].GetDouble(), coordinates[0].GetDouble());
        }

        return (latitude, longitude);
    }

    private static DateTimeOffset? FindTimestamp(JsonElement item, string[] keys)
    {
        if (!TryFind(item, keys, out var value))
        {
            return null;
        }

        // Numeric timestamps are Unix epoch — milliseconds unless the value is small enough to be seconds.
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epoch))
        {
            return Math.Abs(epoch) > 100_000_000_000L
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        var text = JsonPathLite.AsString(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
