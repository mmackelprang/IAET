using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

/// <summary>
/// A single resolved field in a protojson positional structure.
/// </summary>
public sealed record ResolvedField
{
    public required int Position { get; init; }
    public required string DataType { get; init; }
    public string? ResolvedName { get; init; }
    public string? SemanticType { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Low;
    public IReadOnlyList<FieldEvidence> Evidence { get; init; } = [];
    public IReadOnlyList<ResolvedField>? NestedFields { get; init; }
    public bool IsRepeatedEntity { get; init; }
    public string? EntityTypeName { get; init; }
}

/// <summary>
/// Evidence supporting a particular field name resolution.
/// </summary>
public sealed record FieldEvidence
{
    public required string Source { get; init; }
    public required string SuggestedName { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public string? Reasoning { get; init; }
}

/// <summary>
/// Recursively analyzes protojson arrays to produce a tree of resolved fields,
/// detecting nested structures, repeated entities, and generating nested type names.
/// </summary>
public static class RecursiveProtojsonAnalyzer
{
    /// <summary>
    /// Recursively analyze a protojson response body, resolving field names using
    /// value patterns and endpoint context.
    /// </summary>
    public static IReadOnlyList<ResolvedField> Analyze(
        string jsonBody,
        string? endpointPath = null,
        int maxDepth = 5)
    {
        if (string.IsNullOrWhiteSpace(jsonBody))
            return [];

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return [];
        }

        if (root.ValueKind != JsonValueKind.Array)
            return [];

        var domainHints = endpointPath is not null
            ? EndpointContextEnricher.GetDomainHints(endpointPath)
            : [];

        return AnalyzeArray(root, domainHints, "Root", 0, maxDepth);
    }

    /// <summary>
    /// Analyze multiple samples of the same endpoint for better inference.
    /// </summary>
    public static IReadOnlyList<ResolvedField> AnalyzeMultiple(
        IReadOnlyList<string> jsonBodies,
        string? endpointPath = null,
        int maxDepth = 5)
    {
        ArgumentNullException.ThrowIfNull(jsonBodies);

        if (jsonBodies.Count == 0) return [];

        // Analyze each sample
        var allResults = jsonBodies
            .Select(body => Analyze(body, endpointPath, maxDepth))
            .Where(r => r.Count > 0)
            .ToList();

        if (allResults.Count == 0) return [];

        // Merge — use the result with the most fields, enriched by others
        var best = allResults.OrderByDescending(r => CountTotalFields(r)).First();
        return best;
    }

    private static List<ResolvedField> AnalyzeArray(
        JsonElement array,
        IReadOnlyList<string> domainHints,
        string parentName,
        int depth,
        int maxDepth)
    {
        if (depth >= maxDepth) return [];

        var fields = new List<ResolvedField>();
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            fields.Add(AnalyzeElement(element, index, domainHints, parentName, depth, maxDepth));
            index++;
        }

        return fields;
    }

    private static ResolvedField AnalyzeElement(
        JsonElement element,
        int position,
        IReadOnlyList<string> domainHints,
        string parentName,
        int depth,
        int maxDepth)
    {
        var dataType = element.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => element.TryGetInt64(out _) ? "integer" : "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Null or JsonValueKind.Undefined => "null",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            _ => "unknown",
        };

        var evidence = new List<FieldEvidence>();

        // Value pattern matching for strings
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString() ?? "";
            var (semantic, name, conf) = ClassifyStringValue(value);
            if (name is not null)
            {
                evidence.Add(new FieldEvidence
                {
                    Source = "value_pattern",
                    SuggestedName = name,
                    Confidence = conf,
                    Reasoning = $"Value \"{Truncate(value, 40)}\" matches {semantic} pattern",
                });
            }
        }

        // Value pattern matching for numbers
        if (element.ValueKind == JsonValueKind.Number)
        {
            var (semantic, name, conf) = ClassifyNumericValue(element);
            if (name is not null)
            {
                evidence.Add(new FieldEvidence
                {
                    Source = "value_pattern",
                    SuggestedName = name,
                    Confidence = conf,
                    Reasoning = $"Numeric value {element.GetRawText()} matches {semantic}",
                });
            }
        }

        // Domain hint matching
        if (domainHints.Count > position)
        {
            evidence.Add(new FieldEvidence
            {
                Source = "endpoint_context",
                SuggestedName = domainHints[position],
                Confidence = ConfidenceLevel.Low,
                Reasoning = $"Position {position} in endpoint context suggests \"{domainHints[position]}\"",
            });
        }

        // Recursive analysis for arrays
        IReadOnlyList<ResolvedField>? nestedFields = null;
        var isRepeatedEntity = false;
        string? entityTypeName = null;

        if (element.ValueKind == JsonValueKind.Array && depth < maxDepth)
        {
            var arrayLen = element.GetArrayLength();
            if (arrayLen > 0)
            {
                // Check if this is a repeated entity (array of same-shaped arrays/objects)
                isRepeatedEntity = DetectRepeatedEntity(element);

                if (isRepeatedEntity)
                {
                    // Analyze the first item as the entity template
                    var firstItem = element[0];
                    if (firstItem.ValueKind == JsonValueKind.Array)
                    {
                        nestedFields = AnalyzeArray(firstItem, [], $"{parentName}Item", depth + 1, maxDepth);
                        entityTypeName = InferEntityTypeName(nestedFields, parentName, position);
                    }
                }
                else
                {
                    // Analyze as a single nested structure
                    nestedFields = AnalyzeArray(element, [], $"{parentName}_{position}", depth + 1, maxDepth);
                }
            }
        }

        // Pick best name from evidence
        var bestEvidence = evidence
            .OrderBy(e => e.Confidence) // High=0 is best
            .FirstOrDefault();

        return new ResolvedField
        {
            Position = position,
            DataType = dataType,
            ResolvedName = bestEvidence?.SuggestedName,
            SemanticType = evidence.FirstOrDefault(e => e.Source == "value_pattern")?.SuggestedName,
            Confidence = bestEvidence?.Confidence ?? ConfidenceLevel.Low,
            Evidence = evidence,
            NestedFields = nestedFields,
            IsRepeatedEntity = isRepeatedEntity,
            EntityTypeName = entityTypeName,
        };
    }

    private static bool DetectRepeatedEntity(JsonElement array)
    {
        var len = array.GetArrayLength();
        if (len < 2) return false;

        // Check if all items have the same ValueKind
        var firstKind = array[0].ValueKind;
        for (var i = 1; i < len && i < 5; i++) // Check up to 5 items
        {
            if (array[i].ValueKind != firstKind)
                return false;
        }

        // For arrays of arrays, check if they have similar lengths
        if (firstKind == JsonValueKind.Array)
        {
            var firstLen = array[0].GetArrayLength();
            for (var i = 1; i < len && i < 5; i++)
            {
                var itemLen = array[i].GetArrayLength();
                // Allow some variance but similar structure
                if (Math.Abs(itemLen - firstLen) > (firstLen * 0.2) + 2)
                    return false;
            }
            return firstLen > 2; // Only if items have some structure
        }

        // For arrays of objects, same logic
        if (firstKind == JsonValueKind.Object)
            return true;

        // Arrays of primitives are just regular arrays, not repeated entities
        return false;
    }

    private static string InferEntityTypeName(
        IReadOnlyList<ResolvedField>? fields,
        string parentName,
        int position)
    {
        _ = position; // Reserved for future positional heuristics

        if (fields is null || fields.Count == 0)
            return $"{parentName}Item";

        // Look for clues in the resolved field names
        var hasHash = fields.Any(f => f.SemanticType == "deviceHash" || f.SemanticType == "encodedData");
        var hasName = fields.Any(f => f.DataType == "string" && f.Position is 1 or 2);
        var hasCurrency = fields.Any(f => f.SemanticType == "currency");

        if (hasHash && hasName) return "DeviceEntry";
        if (hasCurrency) return "CurrencyAmount";

        return $"{parentName}Entry";
    }

    private static (string Semantic, string? Name, ConfidenceLevel Confidence) ClassifyStringValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return ("empty", null, ConfidenceLevel.Low);

        // Phone number
        if (value.StartsWith('+') && value.Length >= 8 && value.Skip(1).All(char.IsDigit))
            return ("phone_number", "phoneNumber", ConfidenceLevel.High);

        // URL (must come before email since some URLs contain @ characters)
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return ("url", "url", ConfidenceLevel.High);

        // WebSocket URL
        if (value.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            return ("websocket_url", "websocketUrl", ConfidenceLevel.High);

        // SIP URI (must come before email since SIP URIs contain @)
        if (value.StartsWith("sip:", StringComparison.OrdinalIgnoreCase))
            return ("sip_uri", "sipUri", ConfidenceLevel.High);

        // Email
        if (value.Contains('@', StringComparison.Ordinal) &&
            value.Contains('.', StringComparison.Ordinal) &&
            !value.Contains(' ', StringComparison.Ordinal))
            return ("email", "email", ConfidenceLevel.High);

        // IP address
        if (System.Net.IPAddress.TryParse(value, out _) && value.Contains('.', StringComparison.Ordinal))
            return ("ip_address", "ipAddress", ConfidenceLevel.High);

        // UUID
        if (value.Length == 36 && value[8] == '-' && value[13] == '-')
            return ("uuid", "id", ConfidenceLevel.High);

        // Country code (2 chars uppercase)
        if (value.Length == 2 && value.All(c => c is >= 'A' and <= 'Z'))
            return ("country_code", "countryCode", ConfidenceLevel.Medium);

        // Currency code (3 chars uppercase)
        if (value.Length == 3 && value.All(c => c is >= 'A' and <= 'Z'))
            return ("currency", "currency", ConfidenceLevel.Medium);

        // Device hash (64 hex chars)
        if (value.Length == 64 && value.All(char.IsAsciiHexDigit))
            return ("device_hash", "deviceHash", ConfidenceLevel.Medium);

        // Known device names
        if (value is "Android Device" or "Web" or "iPhone" or "iPad")
            return ("device_name", "deviceName", ConfidenceLevel.High);

        // Base64 encoded data (long, has +/= chars)
        if (value.Length > 20 &&
            (value.Contains('=', StringComparison.Ordinal) || value.Contains('+', StringComparison.Ordinal)) &&
            value.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '='))
            return ("encoded_data", "encodedData", ConfidenceLevel.Medium);

        // Short label-like string
        if (value.Length <= 30 &&
            value.Contains(' ', StringComparison.Ordinal) &&
            !value.Contains('\n', StringComparison.Ordinal))
            return ("label", "label", ConfidenceLevel.Low);

        return ("text", null, ConfidenceLevel.Low);
    }

    private static (string Semantic, string? Name, ConfidenceLevel Confidence) ClassifyNumericValue(JsonElement element)
    {
        if (element.TryGetInt64(out var longVal))
        {
            // Enum-like (0-20)
            if (longVal >= 0 && longVal <= 20)
                return ("enum_value", "type", ConfidenceLevel.Low);

            // Timestamp in seconds (2001-2040)
            if (longVal > 1_000_000_000L && longVal < 2_200_000_000L)
                return ("timestamp_seconds", "timestampSec", ConfidenceLevel.Medium);

            // Timestamp in milliseconds (2001-2040)
            if (longVal > 1_000_000_000_000L && longVal < 2_200_000_000_000L)
                return ("timestamp_ms", "timestampMs", ConfidenceLevel.Medium);

            // Currency micro-units (large numbers that are likely amount x 10^8)
            if (longVal > 100_000_000L && longVal < 10_000_000_000L && longVal % 1_000_000 == 0)
                return ("currency_micro", "amountMicro", ConfidenceLevel.Medium);

            // Port number
            if (longVal > 1024 && longVal < 65536)
                return ("port", "port", ConfidenceLevel.Low);
        }

        return ("number", null, ConfidenceLevel.Low);
    }

    private static int CountTotalFields(IReadOnlyList<ResolvedField> fields)
    {
        var count = fields.Count;
        foreach (var f in fields)
        {
            if (f.NestedFields is not null)
                count += CountTotalFields(f.NestedFields);
        }
        return count;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length > maxLength
            ? string.Concat(value.AsSpan(0, maxLength - 3), "...")
            : value;
    }
}
