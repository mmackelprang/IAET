using Iaet.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.Explorer.Api;

/// <summary>
/// Minimal API endpoints for request replay.
/// </summary>
internal static class ReplayApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/replay/{requestId:guid}",
            async (Guid requestId, IEndpointCatalog catalog, IReplayEngine replayEngine, CancellationToken ct) =>
            {
                var request = await catalog.GetRequestByIdAsync(requestId, ct).ConfigureAwait(false);
                if (request is null)
                    return Results.NotFound(new { message = $"Request {requestId} not found." });

                var result = await replayEngine.ReplayAsync(request, ct).ConfigureAwait(false);
                return Results.Ok(result);
            });
    }
}
