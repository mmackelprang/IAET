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
}
