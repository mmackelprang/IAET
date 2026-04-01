using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.JsAnalysis;

namespace Iaet.Schema;

/// <summary>
/// Orchestrates schema inference by analyzing JSON bodies, merging types, and generating output formats.
/// Falls back to protojson positional analysis when standard JSON object inference fails.
/// </summary>
public sealed class JsonSchemaInferrer : ISchemaInferrer
{
    /// <inheritdoc />
    public Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jsonBodies);

        if (jsonBodies.Count == 0)
        {
            return Task.FromResult(new SchemaResult("{}", "", "", []));
        }

        // Try standard JSON object inference first
        var maps = jsonBodies
            .Select(JsonTypeMap.TryAnalyze)
            .Where(m => m is not null)
            .Cast<JsonTypeMap>()
            .ToList();

        if (maps.Count > 0)
        {
            var merged = TypeMerger.Merge(maps);
            var jsonSchema = JsonSchemaGenerator.Generate(merged.MergedMap);
            var csharp = CSharpRecordGenerator.Generate(merged.MergedMap, "InferredResponse");
            var openApi = OpenApiSchemaGenerator.Generate(merged.MergedMap);
            return Task.FromResult(new SchemaResult(jsonSchema, csharp, openApi, merged.Warnings));
        }

        // Fallback: try protojson (positional JSON arrays)
        var protojsonSchemas = jsonBodies
            .Where(ProtojsonAnalyzer.IsProtojson)
            .Select(ProtojsonAnalyzer.Analyze)
            .ToList();

        if (protojsonSchemas.Count > 0)
        {
            var mergedProto = ProtojsonAnalyzer.Merge(protojsonSchemas);
            var description = ProtojsonAnalyzer.Describe(mergedProto);

            // Run value-type inference for semantic field names
            var protojsonBodies = jsonBodies.Where(ProtojsonAnalyzer.IsProtojson).ToList();
            var inferredFields = ValueTypeInferrer.InferFromSamples(protojsonBodies);

            var warnings = new List<string>
            {
                "Response uses protojson format (positional JSON arrays). Field names are unknown — positions inferred from structure.",
            };

            // Generate a positional JSON Schema
            var jsonSchema = GenerateProtojsonSchema(mergedProto);
            // Generate a C# record with positional comments and inferred names
            var csharp = GenerateProtojsonCSharp(mergedProto, inferredFields);
            // Generate valid OpenAPI YAML for protojson
            var openApi = GenerateProtojsonOpenApi(mergedProto);

            return Task.FromResult(new SchemaResult(jsonSchema, csharp, openApi, warnings));
        }

        return Task.FromResult(new SchemaResult("{}", "", "",
            ["No valid JSON response bodies found — bodies may be HTML, binary protobuf, or otherwise non-parseable."]));
    }

    private static string GenerateProtojsonSchema(ProtojsonSchema schema)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"type\": \"array\",");
        sb.AppendLine("  \"description\": \"Protojson positional array\",");
        sb.AppendLine("  \"items\": {");
        sb.AppendLine("    \"oneOf\": [");

        var types = schema.Fields.Select(f => f.InferredType).Distinct().OrderBy(t => t, StringComparer.Ordinal);
        foreach (var type in types)
        {
            var jsonType = type switch
            {
                "string" => "\"string\"",
                "integer" => "\"integer\"",
                "number" => "\"number\"",
                "boolean" => "\"boolean\"",
                "array" => "\"array\"",
                "object" => "\"object\"",
                _ => "\"null\"",
            };
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"      {{ \"type\": {jsonType} }},");
        }

        sb.AppendLine("    ]");
        sb.AppendLine("  },");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"  \"minItems\": {schema.Fields.Count},");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"  \"maxItems\": {schema.Fields.Count}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateProtojsonCSharp(
        ProtojsonSchema schema,
        IReadOnlyList<InferredField>? inferredFields = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// Protojson positional schema — field names are inferred from position");
        sb.AppendLine("// Parse as JsonElement[] and access by index");
        sb.AppendLine("public sealed record InferredResponse(");

        // Build a set of used names to avoid duplicates
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < schema.Fields.Count; i++)
        {
            var field = schema.Fields[i];
            var csType = field.InferredType switch
            {
                "string" => "string?",
                "integer" => "long?",
                "number" => "double?",
                "boolean" => "bool?",
                "array" => "JsonElement[]?",
                "object" => "JsonElement?",
                _ => "JsonElement?",
            };

            // Determine the property name from inferred fields or fall back to positional
            var inferred = inferredFields is not null && i < inferredFields.Count
                ? inferredFields[i]
                : null;
            var propertyName = GetUniquePropertyName(inferred, i, usedNames);
            usedNames.Add(propertyName);

            var comma = i < schema.Fields.Count - 1 ? "," : "";

            // Build comment with semantic info
            var comment = BuildFieldComment(field, inferred);

            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"    {csType} {propertyName}{comma}{comment}");
        }

        sb.AppendLine(");");
        return sb.ToString();
    }

    private static string GetUniquePropertyName(
        InferredField? inferred,
        int position,
        HashSet<string> usedNames)
    {
        if (inferred?.SuggestedName is null)
            return $"Field{position}";

        // Convert camelCase to PascalCase
        var name = char.ToUpperInvariant(inferred.SuggestedName[0]) + inferred.SuggestedName[1..];

        // Deduplicate
        if (usedNames.Contains(name))
        {
            var suffix = 2;
            while (usedNames.Contains($"{name}{suffix}"))
                suffix++;
            name = $"{name}{suffix}";
        }

        return name;
    }

    private static string BuildFieldComment(
        ProtojsonField field,
        InferredField? inferred)
    {
        if (field.NestedArray is not null)
            return $" // nested array with {field.NestedArray.Fields.Count} fields";

        if (inferred is null || inferred.SemanticType is "unknown" or "null")
            return "";

        var parts = new List<string>
        {
            $"{inferred.SemanticType} ({ConfidenceToString(inferred.Confidence)} confidence)",
        };

        if (inferred.SampleValues.Count > 0)
        {
            var sampleText = string.Join(", ", inferred.SampleValues);
            // Truncate if very long
            if (sampleText.Length > 80)
                sampleText = sampleText[..77] + "...";
            parts.Add(inferred.SampleValues.Count == 1
                ? $"sample: \"{sampleText}\""
                : $"values: {sampleText}");
        }

        return $" // {string.Join(" \u2014 ", parts)}";
    }

    private static string ConfidenceToString(ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.High => "high",
        ConfidenceLevel.Medium => "medium",
        ConfidenceLevel.Low => "low",
        _ => "unknown",
    };

    private static string GenerateProtojsonOpenApi(ProtojsonSchema schema)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("type: array");
        sb.AppendLine("description: Protojson positional array — field names unknown, types inferred by position");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"minItems: {schema.Fields.Count}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"maxItems: {schema.Fields.Count}");
        sb.AppendLine("items:");
        sb.AppendLine("  oneOf:");

        var types = schema.Fields.Select(f => f.InferredType).Distinct().OrderBy(t => t, StringComparer.Ordinal);
        foreach (var type in types)
        {
            var yamlType = type switch
            {
                "string" => "string",
                "integer" => "integer",
                "number" => "number",
                "boolean" => "boolean",
                "array" => "array",
                "object" => "object",
                _ => "string",
            };
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"    - type: {yamlType}");
        }

        return sb.ToString();
    }
}
