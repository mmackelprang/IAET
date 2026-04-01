using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ISchemaInferrer
{
    Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default);

    /// <summary>
    /// Infers schema with endpoint context for richer protojson field resolution.
    /// </summary>
    Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, string? endpointPath, CancellationToken ct = default)
        => InferAsync(jsonBodies, ct);

    /// <summary>
    /// Infers schema with endpoint context and optional proto field mappings recovered from
    /// decompiled Android source. Proto mappings take highest priority when naming fields.
    /// Existing callers that omit <paramref name="protoMappings"/> see no behaviour change.
    /// </summary>
    Task<SchemaResult> InferAsync(
        IReadOnlyList<string> jsonBodies,
        string? endpointPath,
        IReadOnlyList<ProtoFieldMappingInfo>? protoMappings,
        CancellationToken ct = default)
        => InferAsync(jsonBodies, endpointPath, ct);
}
