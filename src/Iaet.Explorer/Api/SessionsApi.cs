using Iaet.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.Explorer.Api;

/// <summary>
/// Minimal API endpoints for session browsing.
/// </summary>
internal static class SessionsApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions", async (IEndpointCatalog catalog, CancellationToken ct) =>
        {
            var sessions = await catalog.ListSessionsAsync(ct).ConfigureAwait(false);
            return Results.Ok(sessions);
        });

        app.MapGet("/api/sessions/{id:guid}", async (Guid id, IEndpointCatalog catalog, CancellationToken ct) =>
        {
            var sessions = await catalog.ListSessionsAsync(ct).ConfigureAwait(false);
            var session = sessions.FirstOrDefault(s => s.Id == id);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });
    }
}
