using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ISchemaInferrer
{
    Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default);
}
