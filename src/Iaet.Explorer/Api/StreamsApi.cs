using Iaet.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.Explorer.Api;

/// <summary>
/// Minimal API endpoints for stream browsing.
/// </summary>
internal static class StreamsApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions/{id:guid}/streams",
            async (Guid id, IStreamCatalog streamCatalog, CancellationToken ct) =>
            {
                var streams = await streamCatalog.GetStreamsBySessionAsync(id, ct).ConfigureAwait(false);
                return Results.Ok(streams);
            });
    }
}
