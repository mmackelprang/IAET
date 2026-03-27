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

                try
                {
                    var result = await replayEngine.ReplayAsync(request, ct).ConfigureAwait(false);
                    return Results.Ok(result);
                }
                catch (HttpRequestException ex)
                {
                    return Results.Json(new { message = $"Replay failed: {ex.Message}" }, statusCode: 502);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Json(new { message = ex.Message }, statusCode: 429);
                }
                #pragma warning disable CA1031 // Catch general exception — API boundary must not leak unhandled exceptions
                catch (Exception ex)
                {
                    return Results.Json(new { message = $"Replay error: {ex.Message}" }, statusCode: 500);
                }
                #pragma warning restore CA1031
            });
    }
}
