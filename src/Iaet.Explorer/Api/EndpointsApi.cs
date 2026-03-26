using Iaet.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.Explorer.Api;

/// <summary>
/// Minimal API endpoints for endpoint group and request browsing.
/// </summary>
internal static class EndpointsApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions/{id:guid}/endpoints",
            async (Guid id, IEndpointCatalog catalog, CancellationToken ct) =>
            {
                var groups = await catalog.GetEndpointGroupsAsync(id, ct).ConfigureAwait(false);
                return Results.Ok(groups);
            });

        app.MapGet("/api/sessions/{id:guid}/endpoints/{sig}/requests",
            async (Guid id, string sig, IEndpointCatalog catalog, CancellationToken ct) =>
            {
                var requests = await catalog.GetRequestsBySessionAsync(id, ct).ConfigureAwait(false);
                var decoded = Uri.UnescapeDataString(sig);
                var filtered = requests
                    .Where(r =>
                    {
                        var parts = decoded.Split(' ', 2);
                        if (parts.Length != 2) return false;
                        var method = parts[0];
                        var path = parts[1];
                        return string.Equals(r.HttpMethod, method, StringComparison.OrdinalIgnoreCase)
                            && PathNormalizer.NormalizePath(r.Url).Equals(path, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
                return Results.Ok(filtered);
            });
    }
}
