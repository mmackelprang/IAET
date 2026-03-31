using System.Text.Json;

namespace Iaet.JsAnalysis;

public static class ProtojsonAnalyzer
{
    /// <summary>
    /// Checks if a JSON string is likely protojson format (root-level JSON array).
    /// </summary>
    public static bool IsProtojson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        var trimmed = json.TrimStart();
        if (!trimmed.StartsWith('['))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Analyzes a single protojson response body and returns positional field info.
    /// </summary>
    public static ProtojsonSchema Analyze(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return new ProtojsonSchema { Fields = [] };

        var fields = new List<ProtojsonField>();
        var index = 0;

        foreach (var element in root.EnumerateArray())
        {
            fields.Add(AnalyzeElement(index, element));
            index++;
        }

        return new ProtojsonSchema { Fields = fields };
    }

    /// <summary>
    /// Merges multiple protojson schemas from different samples of the same endpoint.
    /// Positions present in any sample are included; types are unioned.
    /// </summary>
    public static ProtojsonSchema Merge(IReadOnlyList<ProtojsonSchema> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        if (schemas.Count == 0)
            return new ProtojsonSchema { Fields = [] };

        var maxFields = schemas.Max(s => s.Fields.Count);
        var merged = new List<ProtojsonField>();

        for (var i = 0; i < maxFields; i++)
        {
            var types = new HashSet<string>(StringComparer.Ordinal);
            var nullable = false;
            var hasNestedArray = false;
            var hasNestedObject = false;
            var nestedSchemas = new List<ProtojsonSchema>();

            foreach (var schema in schemas)
            {
                if (i >= schema.Fields.Count)
                {
                    nullable = true;
                    continue;
                }

                var field = schema.Fields[i];
                types.Add(field.InferredType);
                nullable = nullable || field.IsNullable;
                hasNestedArray = hasNestedArray || field.NestedArray is not null;
                hasNestedObject = hasNestedObject || field.NestedObject;

                if (field.NestedArray is not null)
                    nestedSchemas.Add(field.NestedArray);
            }

            // Remove "null" from types if we have other types
            if (types.Count > 1)
            {
                types.Remove("null");
                nullable = true;
            }

            merged.Add(new ProtojsonField
            {
                Position = i,
                InferredType = types.Count == 1 ? types.First() : string.Join("|", types.OrderBy(t => t, StringComparer.Ordinal)),
                IsNullable = nullable,
                NestedArray = hasNestedArray && nestedSchemas.Count > 0 ? Merge(nestedSchemas) : null,
                SampleValues = [], // Could collect sample values for better inference
            });
        }

        return new ProtojsonSchema { Fields = merged };
    }

    /// <summary>
    /// Generates a human-readable description of a protojson schema.
    /// </summary>
    public static string Describe(ProtojsonSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Protojson Positional Schema:");
        sb.AppendLine();

        foreach (var field in schema.Fields)
        {
            var nullableTag = field.IsNullable ? " (nullable)" : "";
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"  [{field.Position}] {field.InferredType}{nullableTag}");

            if (field.NestedArray is not null)
            {
                sb.AppendLine("       \u2514\u2500 nested array:");
                foreach (var nested in field.NestedArray.Fields)
                {
                    var nTag = nested.IsNullable ? " (nullable)" : "";
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                        $"          [{nested.Position}] {nested.InferredType}{nTag}");
                }
            }
        }

        return sb.ToString();
    }

    private static ProtojsonField AnalyzeElement(int position, JsonElement element)
    {
        var type = element.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => element.TryGetInt64(out _) ? "integer" : "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Null or JsonValueKind.Undefined => "null",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            _ => "unknown",
        };

        ProtojsonSchema? nestedArray = null;
        if (element.ValueKind == JsonValueKind.Array)
        {
            var nestedFields = new List<ProtojsonField>();
            var i = 0;
            foreach (var child in element.EnumerateArray())
            {
                nestedFields.Add(AnalyzeElement(i, child));
                i++;
            }
            nestedArray = new ProtojsonSchema { Fields = nestedFields };
        }

        return new ProtojsonField
        {
            Position = position,
            InferredType = type,
            IsNullable = type == "null",
            NestedArray = nestedArray,
            NestedObject = element.ValueKind == JsonValueKind.Object,
        };
    }
}

public sealed record ProtojsonSchema
{
    public required IReadOnlyList<ProtojsonField> Fields { get; init; }
}

public sealed record ProtojsonField
{
    public required int Position { get; init; }
    public required string InferredType { get; init; }
    public bool IsNullable { get; init; }
    public ProtojsonSchema? NestedArray { get; init; }
    public bool NestedObject { get; init; }
    public IReadOnlyList<string> SampleValues { get; init; } = [];
}
