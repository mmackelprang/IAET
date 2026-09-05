using Iaet.DukeEnergy.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.DukeEnergy.Api;

/// <summary>
/// The combined "is my power out?" endpoint.
/// </summary>
internal static class HomeApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/home/status", async (
            IHomeOutageService service,
            CancellationToken cancellationToken) =>
        {
            var status = await service.GetHomeStatusAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(status);
        })
        .WithName("GetHomeStatus")
        .WithSummary("Combines the account outage status, nearby outages, and the county rollup for the configured home.");
    }
}
