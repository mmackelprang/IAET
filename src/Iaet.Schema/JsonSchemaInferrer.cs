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
            var warnings = new List<string>
            {
                "Response uses protojson format (positional JSON arrays). Field names are unknown — positions inferred from structure.",
            };

            // Generate a positional JSON Schema
            var jsonSchema = GenerateProtojsonSchema(mergedProto);
            // Generate a C# record with positional comments
            var csharp = GenerateProtojsonCSharp(mergedProto);
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

    private static string GenerateProtojsonCSharp(ProtojsonSchema schema)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// Protojson positional schema — field names are inferred from position");
        sb.AppendLine("// Parse as JsonElement[] and access by index");
        sb.AppendLine("public sealed record InferredResponse(");

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
            var comma = i < schema.Fields.Count - 1 ? "," : "";
            var nested = field.NestedArray is not null
                ? $" // nested array with {field.NestedArray.Fields.Count} fields"
                : "";
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"    {csType} Field{i}{comma}{nested}");
        }

        sb.AppendLine(");");
        return sb.ToString();
    }

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
