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
        => InferAsync(jsonBodies, endpointPath: null, protoMappings: null, ct);

    /// <inheritdoc />
    public Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, string? endpointPath, CancellationToken ct = default)
        => InferAsync(jsonBodies, endpointPath, protoMappings: null, ct);

    /// <inheritdoc />
    public Task<SchemaResult> InferAsync(
        IReadOnlyList<string> jsonBodies,
        string? endpointPath,
        IReadOnlyList<ProtoFieldMappingInfo>? protoMappings,
        CancellationToken ct = default)
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

            // Run value-type inference for semantic field names
            var protojsonBodies = jsonBodies.Where(ProtojsonAnalyzer.IsProtojson).ToList();
            var inferredFields = ValueTypeInferrer.InferFromSamples(protojsonBodies);

            // Run recursive analyzer for deeper field resolution with endpoint context
            var resolvedFields = RecursiveProtojsonAnalyzer.AnalyzeMultiple(protojsonBodies, endpointPath);

            var warnings = new List<string>
            {
                "Response uses protojson format (positional JSON arrays). Field names are unknown — positions inferred from structure.",
            };

            // Generate a positional JSON Schema
            var jsonSchema = GenerateProtojsonSchema(mergedProto);
            // Generate a C# record with positional comments, inferred names, and nested types
            // Proto mappings (source-code evidence) take highest priority when available
            var csharp = GenerateProtojsonCSharp(mergedProto, inferredFields, resolvedFields, protoMappings);
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
        IReadOnlyList<InferredField>? inferredFields = null,
        IReadOnlyList<ResolvedField>? resolvedFields = null,
        IReadOnlyList<ProtoFieldMappingInfo>? protoMappings = null)
    {
        // Build a fast lookup: position -> highest-confidence proto mapping
        // Priority order: field_constant > getter > position_access > descriptor
        var protoByPosition = BuildProtoMappingLookup(protoMappings);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// Protojson positional schema — field names are inferred from position");
        sb.AppendLine("// Parse as JsonElement[] and access by index");
        sb.AppendLine("public sealed record InferredResponse(");

        // Build a set of used names to avoid duplicates
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        // Collect nested type definitions to append after the main record
        var nestedTypes = new List<string>();

        for (var i = 0; i < schema.Fields.Count; i++)
        {
            var field = schema.Fields[i];
            var resolved = resolvedFields is not null && i < resolvedFields.Count
                ? resolvedFields[i]
                : null;

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

            // If the resolved field has nested fields, generate a nested type
            if (resolved?.NestedFields is { Count: > 0 })
            {
                var nestedTypeName = resolved.IsRepeatedEntity && resolved.EntityTypeName is not null
                    ? resolved.EntityTypeName
                    : $"Field{i}Item";
                csType = resolved.IsRepeatedEntity
                    ? $"{nestedTypeName}[]?"
                    : $"{nestedTypeName}?";
                nestedTypes.Add(GenerateNestedRecord(nestedTypeName, resolved.NestedFields));
            }

            // Determine the property name using priority order:
            // 1. ProtoFieldMapper (source code — highest confidence)
            // 2. RecursiveProtojsonAnalyzer resolved name (endpoint context + value patterns)
            // 3. ValueTypeInferrer suggested name (value pattern matching)
            // 4. Fallback: Field{i}
            string propertyName;
            string? protoSource = null;

            if (protoByPosition.TryGetValue(i, out var protoMapping))
            {
                // Highest priority: source-code evidence from decompiled Java
                var name = char.ToUpperInvariant(protoMapping.SuggestedName[0]) + protoMapping.SuggestedName[1..];
                propertyName = EnsureUnique(name, usedNames);
                protoSource = protoMapping.Source;
            }
            else if (resolved?.ResolvedName is not null)
            {
                var name = char.ToUpperInvariant(resolved.ResolvedName[0]) + resolved.ResolvedName[1..];
                propertyName = EnsureUnique(name, usedNames);
            }
            else
            {
                var inferred = inferredFields is not null && i < inferredFields.Count
                    ? inferredFields[i]
                    : null;
                propertyName = GetUniquePropertyName(inferred, i, usedNames);
            }
            usedNames.Add(propertyName);

            var comma = i < schema.Fields.Count - 1 ? "," : "";

            // Build comment: proto-sourced fields get a comment indicating origin
            var inferred2 = inferredFields is not null && i < inferredFields.Count
                ? inferredFields[i]
                : null;
            var comment = protoSource is not null
                ? $" // proto:{protoSource} (high confidence)"
                : BuildFieldComment(field, inferred2);

            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"    {csType} {propertyName}{comma}{comment}");
        }

        sb.AppendLine(");");

        // Append nested type definitions
        foreach (var nested in nestedTypes)
        {
            sb.AppendLine();
            sb.Append(nested);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a dictionary from position to the single best proto mapping for that position.
    /// When multiple mappings exist for the same position, prefers higher-confidence evidence
    /// and breaks ties by source category priority (field_constant > getter > position_access).
    /// </summary>
    private static Dictionary<int, ProtoFieldMappingInfo> BuildProtoMappingLookup(
        IReadOnlyList<ProtoFieldMappingInfo>? protoMappings)
    {
        var result = new Dictionary<int, ProtoFieldMappingInfo>();
        if (protoMappings is null || protoMappings.Count == 0)
            return result;

        foreach (var mapping in protoMappings)
        {
            // Descriptor entries (position == -1) apply to the message as a whole — skip
            if (mapping.Position < 0)
                continue;

            if (!result.TryGetValue(mapping.Position, out var existing))
            {
                result[mapping.Position] = mapping;
                continue;
            }

            // Replace if new mapping has higher confidence, or same confidence but higher source priority
            if (IsHigherPriority(mapping, existing))
                result[mapping.Position] = mapping;
        }

        return result;
    }

    private static bool IsHigherPriority(ProtoFieldMappingInfo candidate, ProtoFieldMappingInfo current)
    {
        if (candidate.Confidence < current.Confidence) // High=0 < Medium=1 < Low=2
            return true;
        if (candidate.Confidence > current.Confidence)
            return false;
        // Same confidence: prefer by source category
        return SourcePriority(candidate.Source) < SourcePriority(current.Source);
    }

    private static int SourcePriority(string source) => source switch
    {
        "field_constant" => 0,
        "getter"         => 1,
        "position_access"=> 2,
        _                => 3,
    };

    private static string GenerateNestedRecord(
        string typeName,
        IReadOnlyList<ResolvedField> fields)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"public sealed record {typeName}(");

        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            var csType = f.DataType switch
            {
                "string" => "string?",
                "integer" => "long?",
                "number" => "double?",
                "boolean" => "bool?",
                "array" => "JsonElement[]?",
                "object" => "JsonElement?",
                "null" => "JsonElement?",
                _ => "JsonElement?",
            };

            var name = f.ResolvedName is not null
                ? char.ToUpperInvariant(f.ResolvedName[0]) + f.ResolvedName[1..]
                : $"Field{i}";
            name = EnsureUnique(name, usedNames);
            usedNames.Add(name);

            var comma = i < fields.Count - 1 ? "," : "";
            var comment = f.SemanticType is not null ? $" // {f.SemanticType}" : "";

            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"    {csType} {name}{comma}{comment}");
        }

        sb.AppendLine(");");
        return sb.ToString();
    }

    private static string EnsureUnique(string name, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(name))
            return name;

        var suffix = 2;
        while (usedNames.Contains($"{name}{suffix}"))
            suffix++;
        return $"{name}{suffix}";
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
