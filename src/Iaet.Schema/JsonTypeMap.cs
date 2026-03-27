using System.Text.Json;

namespace Iaet.Schema;

/// <summary>
/// Identifies the JSON type of a field value.
/// </summary>
#pragma warning disable CA1720 // Enum members mirror JSON type names by design
public enum JsonFieldType
{
    String,
    Number,
    Boolean,
    Null,
    Object,
    Array,
}
#pragma warning restore CA1720

/// <summary>
/// Describes the structural type of a single JSON field, including nested objects and array items.
/// </summary>
public sealed record FieldInfo
{
    public required JsonFieldType JsonType { get; init; }
    public Dictionary<string, FieldInfo>? NestedFields { get; init; }
    public FieldInfo? ArrayItemType { get; init; }
    public bool IsNullable { get; init; }
}

/// <summary>
/// Analyzes a JSON string and produces a structural map of field names to their types.
/// </summary>
public sealed class JsonTypeMap
{
    /// <summary>
    /// Gets the top-level fields discovered in the JSON body.
    /// </summary>
    public IReadOnlyDictionary<string, FieldInfo> Fields { get; }

    private JsonTypeMap(Dictionary<string, FieldInfo> fields)
    {
        Fields = fields;
    }

    /// <summary>
    /// Parses a JSON string and builds a <see cref="JsonTypeMap"/> describing the structure.
    /// </summary>
    /// <param name="json">A JSON object string.</param>
    /// <returns>A <see cref="JsonTypeMap"/> representing the field structure.</returns>
    public static JsonTypeMap Analyze(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var fields = AnalyzeObject(doc.RootElement);
        return new JsonTypeMap(fields);
    }

    /// <summary>
    /// Attempts to parse a JSON string. Returns <see langword="null"/> if the body is not valid JSON
    /// (e.g. HTML, protobuf, JSONP, or BOM-prefixed content).
    /// </summary>
    public static JsonTypeMap? TryAnalyze(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            var fields = AnalyzeObject(doc.RootElement);
            return new JsonTypeMap(fields);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a <see cref="JsonTypeMap"/> from an existing field dictionary.
    /// Used internally for merging.
    /// </summary>
    internal static JsonTypeMap FromFields(Dictionary<string, FieldInfo> fields)
    {
        return new JsonTypeMap(fields);
    }

    private static Dictionary<string, FieldInfo> AnalyzeObject(JsonElement element)
    {
        var fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            fields[property.Name] = AnalyzeElement(property.Value);
        }

        return fields;
    }

    private static FieldInfo AnalyzeElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => new FieldInfo { JsonType = JsonFieldType.String },
            JsonValueKind.Number => new FieldInfo { JsonType = JsonFieldType.Number },
            JsonValueKind.True or JsonValueKind.False => new FieldInfo { JsonType = JsonFieldType.Boolean },
            JsonValueKind.Null => new FieldInfo { JsonType = JsonFieldType.Null, IsNullable = true },
            JsonValueKind.Object => AnalyzeObjectElement(element),
            JsonValueKind.Array => AnalyzeArrayElement(element),
            _ => new FieldInfo { JsonType = JsonFieldType.String },
        };
    }

    private static FieldInfo AnalyzeObjectElement(JsonElement element)
    {
        var nested = AnalyzeObject(element);
        return new FieldInfo
        {
            JsonType = JsonFieldType.Object,
            NestedFields = nested,
        };
    }

    private static FieldInfo AnalyzeArrayElement(JsonElement element)
    {
        FieldInfo? itemType = null;

        foreach (var item in element.EnumerateArray())
        {
            var current = AnalyzeElement(item);
            itemType = itemType is null ? current : MergeFieldInfo(itemType, current);
        }

        return new FieldInfo
        {
            JsonType = JsonFieldType.Array,
            ArrayItemType = itemType,
        };
    }

    /// <summary>
    /// Merges two <see cref="FieldInfo"/> instances, combining nested fields and handling type conflicts.
    /// </summary>
    internal static FieldInfo MergeFieldInfo(FieldInfo a, FieldInfo b)
    {
        // If either is null-type, adopt the other's type but mark nullable
        if (a.JsonType == JsonFieldType.Null)
        {
            return b with { IsNullable = true };
        }

        if (b.JsonType == JsonFieldType.Null)
        {
            return a with { IsNullable = true };
        }

        // If types differ (and neither is null), keep the first type but mark nullable
        if (a.JsonType != b.JsonType)
        {
            return a with { IsNullable = true };
        }

        // Same type — merge nested structures
        if (a.JsonType == JsonFieldType.Object && a.NestedFields is not null && b.NestedFields is not null)
        {
            var merged = new Dictionary<string, FieldInfo>(a.NestedFields, StringComparer.Ordinal);
            foreach (var (key, value) in b.NestedFields)
            {
                if (merged.TryGetValue(key, out var existing))
                {
                    merged[key] = MergeFieldInfo(existing, value);
                }
                else
                {
                    merged[key] = value;
                }
            }

            return a with { NestedFields = merged };
        }

        if (a.JsonType == JsonFieldType.Array && a.ArrayItemType is not null && b.ArrayItemType is not null)
        {
            return a with { ArrayItemType = MergeFieldInfo(a.ArrayItemType, b.ArrayItemType) };
        }

        return a;
    }
}
