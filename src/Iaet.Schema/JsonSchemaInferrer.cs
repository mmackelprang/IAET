using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Schema;

/// <summary>
/// Orchestrates schema inference by analyzing JSON bodies, merging types, and generating output formats.
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

        var maps = jsonBodies.Select(JsonTypeMap.Analyze).ToList();
        var merged = TypeMerger.Merge(maps);

        var jsonSchema = JsonSchemaGenerator.Generate(merged.MergedMap);
        var csharp = CSharpRecordGenerator.Generate(merged.MergedMap, "InferredResponse");
        var openApi = OpenApiSchemaGenerator.Generate(merged.MergedMap);

        return Task.FromResult(new SchemaResult(jsonSchema, csharp, openApi, merged.Warnings));
    }
}
