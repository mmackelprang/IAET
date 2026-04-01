using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

/// <summary>
/// Represents a single inferred field from protojson value analysis.
/// </summary>
public sealed record InferredField
{
    public required int Position { get; init; }
    public required string DataType { get; init; }
    public required string SemanticType { get; init; }
    public string? SuggestedName { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
    public IReadOnlyList<string> SampleValues { get; init; } = [];
}

/// <summary>
/// Infers semantic field types from actual protojson values.
/// Analyzes values across multiple samples to determine what each
/// positional field likely represents.
/// </summary>
public static partial class ValueTypeInferrer
{
    public static IReadOnlyList<InferredField> InferFromSamples(IReadOnlyList<string> jsonBodies)
    {
        ArgumentNullException.ThrowIfNull(jsonBodies);

        var samples = new List<JsonElement>();
        foreach (var body in jsonBodies)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    samples.Add(doc.RootElement.Clone());
            }
            catch (JsonException) { /* skip unparseable bodies */ }
        }

        if (samples.Count == 0)
            return [];

        var maxFields = samples.Max(s => s.GetArrayLength());
        var results = new List<InferredField>();

        for (var pos = 0; pos < maxFields; pos++)
        {
            var values = new List<string>();
            var kinds = new HashSet<JsonValueKind>();

            foreach (var sample in samples)
            {
                if (pos >= sample.GetArrayLength())
                    continue;

                var element = sample[pos];
                kinds.Add(element.ValueKind);

                if (element.ValueKind == JsonValueKind.String)
                {
                    var val = element.GetString();
                    if (val is not null && val.Length <= 200)
                        values.Add(val);
                }
                else if (element.ValueKind == JsonValueKind.Number)
                {
                    values.Add(element.GetRawText());
                }
                else if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    values.Add(element.GetRawText());
                }
            }

            var dataType = DetermineDataType(kinds);
            var (semanticType, suggestedName, confidence) = ClassifyValues(values, dataType);

            results.Add(new InferredField
            {
                Position = pos,
                DataType = dataType,
                SemanticType = semanticType,
                SuggestedName = suggestedName,
                Confidence = confidence,
                SampleValues = values.Take(3).ToList(),
            });
        }

        return results;
    }

    private static string DetermineDataType(HashSet<JsonValueKind> kinds)
    {
        var normalizedTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in kinds)
        {
            switch (kind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    normalizedTypes.Add("boolean");
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    normalizedTypes.Add("null");
                    break;
                case JsonValueKind.String:
                    normalizedTypes.Add("string");
                    break;
                case JsonValueKind.Number:
                    normalizedTypes.Add("number");
                    break;
                case JsonValueKind.Array:
                    normalizedTypes.Add("array");
                    break;
                case JsonValueKind.Object:
                    normalizedTypes.Add("object");
                    break;
                default:
                    normalizedTypes.Add("unknown");
                    break;
            }
        }

        // Remove null if we have concrete types
        if (normalizedTypes.Count > 1)
            normalizedTypes.Remove("null");

        return normalizedTypes.Count == 1
            ? normalizedTypes.First()
            : "mixed";
    }

    private static (string SemanticType, string? SuggestedName, ConfidenceLevel Confidence) ClassifyValues(
        List<string> values, string dataType)
    {
        // Handle types that produce no collected values (null, array, object)
        if (dataType == "null")
            return ("null", null, ConfidenceLevel.Low);

        if (dataType == "array")
            return ("array", "items", ConfidenceLevel.Low);

        if (dataType == "object")
            return ("object", "data", ConfidenceLevel.Low);

        if (values.Count == 0)
            return ("unknown", null, ConfidenceLevel.Low);

        // Check string patterns
        if (dataType == "string")
            return ClassifyStringValues(values);

        if (dataType == "boolean")
            return ("boolean", "enabled", ConfidenceLevel.Medium);

        if (dataType == "number")
            return ClassifyNumberValues(values);

        return ("unknown", null, ConfidenceLevel.Low);
    }

    private static (string SemanticType, string? SuggestedName, ConfidenceLevel Confidence) ClassifyStringValues(
        List<string> values)
    {
        if (values.All(v => IpAddressPattern().IsMatch(v)))
            return ("ip_address", "ipAddress", ConfidenceLevel.Medium);

        if (values.All(v => EmailPattern().IsMatch(v)))
            return ("email", "email", ConfidenceLevel.High);

        if (values.All(v => PhonePattern().IsMatch(v)))
            return ("phone_number", "phoneNumber", ConfidenceLevel.High);

        if (values.All(v => v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return ("url", "url", ConfidenceLevel.High);

        if (values.All(v => UuidPattern().IsMatch(v)))
            return ("uuid", "id", ConfidenceLevel.High);

        if (values.All(v => IsoTimestampPattern().IsMatch(v)))
            return ("timestamp", "timestamp", ConfidenceLevel.High);

        if (values.All(v => v.Length == 2 && string.Equals(v, v.ToUpperInvariant(), StringComparison.Ordinal) && v.All(char.IsAsciiLetter)))
            return ("country_code", "countryCode", ConfidenceLevel.Medium);

        if (values.All(v => CurrencyPattern().IsMatch(v)))
            return ("currency", "currency", ConfidenceLevel.Medium);

        if (values.All(v => LocalePattern().IsMatch(v)))
            return ("locale", "locale", ConfidenceLevel.Medium);

        if (values.All(v => Base64Pattern().IsMatch(v) && v.Length > 20))
            return ("encoded_data", "encodedData", ConfidenceLevel.Medium);

        if (values.All(v => v.StartsWith('{') || v.StartsWith('[')))
            return ("json_payload", "jsonPayload", ConfidenceLevel.Medium);

        // Generic string - try to infer from content
        if (values.All(v => v.Length <= 50 && !v.Contains(' ', StringComparison.Ordinal)))
            return ("identifier", "identifier", ConfidenceLevel.Low);

        return ("text", "text", ConfidenceLevel.Low);
    }

    private static (string SemanticType, string? SuggestedName, ConfidenceLevel Confidence) ClassifyNumberValues(
        List<string> values)
    {
        var allInts = values.All(v => !v.Contains('.', StringComparison.Ordinal));
        if (allInts)
        {
            var intValues = values
                .Select(v => long.TryParse(v, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .ToList();

            if (intValues.All(v => v >= 0 && v <= 10))
                return ("enum_value", "type", ConfidenceLevel.Medium);
            if (intValues.All(v => v > 1_000_000_000_000L))
                return ("timestamp_ms", "timestampMs", ConfidenceLevel.Medium);
            if (intValues.All(v => v > 1_000_000_000L && v < 2_000_000_000L))
                return ("timestamp_s", "timestampSec", ConfidenceLevel.Medium);

            return ("integer", "count", ConfidenceLevel.Low);
        }

        return ("decimal", "value", ConfidenceLevel.Low);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+\d[\d\s\-]{7,}$|^\(\d{3}\)\s*\d{3}[-.\s]\d{4}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$")]
    private static partial Regex IpAddressPattern();

    [GeneratedRegex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase)]
    private static partial Regex UuidPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}")]
    private static partial Regex IsoTimestampPattern();

    [GeneratedRegex(@"^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();

    [GeneratedRegex(@"^[a-z]{2}[-_][A-Z]{2}$")]
    private static partial Regex LocalePattern();

    [GeneratedRegex(@"^[A-Za-z0-9+/=]{4,}$")]
    private static partial Regex Base64Pattern();
}
