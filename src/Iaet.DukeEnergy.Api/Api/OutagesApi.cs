using Iaet.DukeEnergy.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.DukeEnergy.Api;

/// <summary>
/// Read-only endpoints over Duke Energy's public outage map.
/// </summary>
internal static class OutagesApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/jurisdictions", () => Results.Ok(Jurisdictions.All))
           .WithName("ListJurisdictions")
           .WithSummary("Lists the Duke Energy operating-company codes this service understands.");

        app.MapGet("/api/v1/outages", async (
            string? jurisdiction,
            IOutageMapClient client,
            CancellationToken cancellationToken) =>
        {
            return await UpstreamAsync(
                async () => Results.Ok(await client.GetOutagesAsync(jurisdiction, cancellationToken).ConfigureAwait(false)))
                .ConfigureAwait(false);
        })
        .WithName("ListOutages")
        .WithSummary("Lists every outage the map reports for a jurisdiction.");

        app.MapGet("/api/v1/outages/counties", async (
            string? jurisdiction,
            IOutageMapClient client,
            CancellationToken cancellationToken) =>
        {
            return await UpstreamAsync(
                async () => Results.Ok(await client.GetCountiesAsync(jurisdiction, cancellationToken).ConfigureAwait(false)))
                .ConfigureAwait(false);
        })
        .WithName("ListCountyOutages")
        .WithSummary("Lists the per-county outage rollup for a jurisdiction.");

        app.MapGet("/api/v1/outages/neighborhood", async (
            double? lat,
            double? lon,
            double? radiusMiles,
            string? jurisdiction,
            IOutageMapClient client,
            DukeEnergyOptions options,
            CancellationToken cancellationToken) =>
        {
            var latitude  = lat ?? options.Home.Latitude;
            var longitude = lon ?? options.Home.Longitude;

            if (latitude is null || longitude is null)
            {
                return Results.Problem(
                    title: "No location to search around",
                    detail: "Pass lat and lon, or configure DukeEnergy:Home:Latitude and DukeEnergy:Home:Longitude.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var radius = radiusMiles ?? options.Home.RadiusMiles;
            if (radius <= 0)
            {
                return Results.Problem(
                    title: "Invalid radius",
                    detail: "radiusMiles must be greater than zero.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return await UpstreamAsync(async () => Results.Ok(
                await client
                    .GetNeighborhoodAsync(
                        latitude.Value,
                        longitude.Value,
                        radius,
                        jurisdiction ?? options.Home.Jurisdiction,
                        cancellationToken)
                    .ConfigureAwait(false)))
                .ConfigureAwait(false);
        })
        .WithName("GetNeighborhoodOutages")
        .WithSummary("Lists outages within a radius of a point, nearest first. Defaults to the configured home location.");

        app.MapGet("/api/v1/outages/at-address", async (
            string? address,
            double? radiusMiles,
            string? jurisdiction,
            IAddressOutageService service,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return Results.Problem(
                    title: "Missing address",
                    detail: "address is required, for example ?address=123 Main St, Raleigh, NC 27601.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (radiusMiles is <= 0)
            {
                return Results.Problem(
                    title: "Invalid radius",
                    detail: "radiusMiles must be greater than zero.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return await UpstreamAsync(async () =>
            {
                var report = await service
                    .GetByAddressAsync(address, radiusMiles, jurisdiction, cancellationToken)
                    .ConfigureAwait(false);

                return report is null
                    ? Results.Problem(
                        title: "Address could not be located",
                        detail: $"The geocoder found no match for '{address}'. Include the city, state and ZIP.",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(report);
            }).ConfigureAwait(false);
        })
        .WithName("GetOutagesAtAddress")
        .WithSummary("Geocodes a street address and reports outages around it. Proximity only — not a per-meter status.");
    }

    /// <summary>
    /// Reports a failure reaching Duke Energy as a gateway error rather than letting it surface as
    /// an unhandled 500, so a caller can tell "Duke is unreachable" from "this service is broken".
    /// </summary>
    private static async Task<IResult> UpstreamAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem(
                title: "Duke Energy or the geocoder is unreachable",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (TaskCanceledException ex)
        {
            return Results.Problem(
                title: "Duke Energy or the geocoder timed out",
                detail: ex.Message,
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (InvalidOperationException ex)
        {
            // Raised when the outage-map configuration document no longer yields credentials.
            return Results.Problem(
                title: "Duke Energy outage map could not be authenticated",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
