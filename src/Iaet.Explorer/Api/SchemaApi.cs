using Iaet.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.Explorer.Api;

/// <summary>
/// Minimal API endpoints for schema inference.
/// </summary>
internal static class SchemaApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions/{id:guid}/endpoints/{sig}/schema",
            async (Guid id, string sig, IEndpointCatalog catalog, ISchemaInferrer schemaInferrer, CancellationToken ct) =>
            {
                var decoded = Uri.UnescapeDataString(sig);
                var bodies = await catalog.GetResponseBodiesAsync(id, decoded, ct).ConfigureAwait(false);
                if (bodies.Count == 0)
                    return Results.NotFound(new { message = "No response bodies found for this endpoint." });

                var schema = await schemaInferrer.InferAsync(bodies, ct).ConfigureAwait(false);
                return Results.Ok(schema);
            });
    }
}
