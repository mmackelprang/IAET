using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.DukeEnergy.Api;

/// <summary>
/// Account-scoped endpoints: resolve an account, and read or file its outage.
/// </summary>
internal static class AccountsApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/accounts/lookup", async (
            AccountLookupRequest request,
            IOutageReportClient client,
            CancellationToken cancellationToken) =>
        {
            if (!client.IsConfigured)
            {
                return NotConfigured(client);
            }

            if (string.IsNullOrWhiteSpace(request?.PhoneNumber))
            {
                return Results.Problem(
                    title: "Missing phone number",
                    detail: "phoneNumber is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await client
                .LookupAccountByPhoneAsync(request.PhoneNumber, cancellationToken)
                .ConfigureAwait(false);

            return result.Found ? Results.Ok(result) : Results.NotFound(result);
        })
        .WithName("LookupAccount")
        .WithSummary("Resolves a Duke Energy account from a phone number.");

        app.MapGet("/api/v1/accounts/{accountNumber}", async (
            string accountNumber,
            IOutageReportClient client,
            CancellationToken cancellationToken) =>
        {
            if (!client.IsConfigured)
            {
                return NotConfigured(client);
            }

            var result = await client
                .LookupAccountByNumberAsync(accountNumber, cancellationToken)
                .ConfigureAwait(false);

            return result.Found ? Results.Ok(result) : Results.NotFound(result);
        })
        .WithName("GetAccount")
        .WithSummary("Resolves an account from its account number, including the authoritative service address.");

        app.MapGet("/api/v1/accounts/{accountNumber}/outage", async (
            string accountNumber,
            IOutageReportClient client,
            CancellationToken cancellationToken) =>
        {
            if (!client.IsConfigured)
            {
                return NotConfigured(client);
            }

            var status = await client
                .GetExistingOutageAsync(accountNumber, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(status);
        })
        .WithName("GetAccountOutage")
        .WithSummary("Reads the outage Duke Energy currently has on file for an account.");

        app.MapPost("/api/v1/outages/report", async (
            OutageReportRequest request,
            IOutageReportClient client,
            CancellationToken cancellationToken) =>
        {
            if (!client.IsConfigured)
            {
                return NotConfigured(client);
            }

            if (string.IsNullOrWhiteSpace(request?.AccountNumber))
            {
                return Results.Problem(
                    title: "Missing account number",
                    detail: "accountNumber is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var receipt = await client.SubmitReportAsync(request, cancellationToken).ConfigureAwait(false);
                return receipt.Accepted || receipt.DryRun
                    ? Results.Ok(receipt)
                    : Results.Json(receipt, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (InvalidOperationException ex)
            {
                // Thrown by the client's own safety gates: submission disabled, wrong account, or
                // the daily submission cap. These are the caller's problem, not a server fault.
                return Results.Problem(
                    title: "Outage report refused",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("ReportOutage")
        .WithSummary("Files a new outage report for the configured account.");
    }

    private static IResult NotConfigured(IOutageReportClient client) => Results.Problem(
        title: "Outage-report flow is not configured",
        detail: client.ConfigurationProblem,
        statusCode: StatusCodes.Status503ServiceUnavailable);
}

/// <summary>Request body for account lookup.</summary>
/// <param name="PhoneNumber">Phone number on the Duke Energy account.</param>
public sealed record AccountLookupRequest(string PhoneNumber);
