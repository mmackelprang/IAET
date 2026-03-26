using System.CommandLine;
using System.Globalization;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class ReplayCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var replayCmd = new Command("replay", "Replay captured HTTP requests and compare responses");

        var dryRunOption = new Option<bool>("--dry-run") { Description = "Print what would be replayed without sending HTTP requests" };

        // ── run ────────────────────────────────────────────────────────────────
        var requestIdOption = new Option<Guid>("--request-id") { Description = "Request ID to replay", Required = true };

        var runCmd = new Command("run", "Replay a single captured request");
        runCmd.Add(requestIdOption);
        runCmd.Add(dryRunOption);

        runCmd.SetAction(async (parseResult) =>
        {
            var requestId = parseResult.GetRequiredValue(requestIdOption);
            var dryRun    = parseResult.GetValue(dryRunOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);

            var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var request = await catalog.GetRequestByIdAsync(requestId).ConfigureAwait(false);

            if (request is null)
            {
                Console.WriteLine($"Request {requestId} not found.");
                return;
            }

            if (dryRun)
            {
                PrintDryRunRequest(request);
                return;
            }

            var engine = scope.ServiceProvider.GetRequiredService<IReplayEngine>();
            var result = await engine.ReplayAsync(request).ConfigureAwait(false);

            PrintReplayResult(request, result);
        });

        // ── batch ──────────────────────────────────────────────────────────────
        var sessionIdOption = new Option<Guid>("--session-id") { Description = "Session ID", Required = true };

        var batchCmd = new Command("batch", "Replay one representative request per unique endpoint in a session");
        batchCmd.Add(sessionIdOption);
        batchCmd.Add(dryRunOption);

        batchCmd.SetAction(async (parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionIdOption);
            var dryRun    = parseResult.GetValue(dryRunOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);

            var catalog  = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var requests = await catalog.GetRequestsBySessionAsync(sessionId).ConfigureAwait(false);

            if (requests.Count == 0)
            {
                Console.WriteLine("No requests found for session.");
                return;
            }

            // Group by normalized endpoint; take first request per group
            var representatives = requests
                .GroupBy(r => NormalizeEndpoint(r.HttpMethod, r.Url))
                .Select(g => g.OrderBy(r => r.Timestamp).First())
                .ToList();

            if (dryRun)
            {
                Console.WriteLine($"Dry-run: would replay {representatives.Count} representative request(s).");
                Console.WriteLine();
                Console.WriteLine($"{"#",-4} {"Method",-8} {"URL",-60}");
                Console.WriteLine(new string('-', 75));
                var i = 1;
                foreach (var req in representatives)
                {
                    Console.WriteLine($"{i,-4} {req.HttpMethod,-8} {req.Url,-60}");
                    i++;
                }

                return;
            }

            var engine = scope.ServiceProvider.GetRequiredService<IReplayEngine>();

            Console.WriteLine($"Replaying {representatives.Count} representative request(s)...");
            Console.WriteLine();
            Console.WriteLine($"{"#",-4} {"Method",-8} {"Status",-8} {"Diffs",-8} {"Ms",-8} {"URL"}");
            Console.WriteLine(new string('-', 100));

            var j = 1;
            foreach (var req in representatives)
            {
                ReplayResult result;
                try
                {
                    result = await engine.ReplayAsync(req).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"{j,-4} {"ERROR",-8} {"N/A",-8} {"N/A",-8} {"N/A",-8} {req.Url}  [{ex.Message}]");
                    j++;
                    continue;
                }

                var diffMark = result.Diffs.Count == 0 ? "OK" : result.Diffs.Count.ToString(CultureInfo.InvariantCulture);
                Console.WriteLine($"{j,-4} {req.HttpMethod,-8} {result.ResponseStatus,-8} {diffMark,-8} {result.DurationMs,-8} {req.Url}");
                j++;
            }
        });

        replayCmd.Add(runCmd);
        replayCmd.Add(batchCmd);
        return replayCmd;
    }

    private static string NormalizeEndpoint(string method, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"{method.ToUpperInvariant()} {url}";
        }

        var path     = uri.AbsolutePath;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join("/",
            segments.Select(s => IsId(s) ? "{id}" : s));
        return $"{method.ToUpperInvariant()} /{normalized}";
    }

    private static bool IsId(string segment) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            segment,
            @"^(\d+|[0-9a-f]{8,}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static void PrintDryRunRequest(CapturedRequest request)
    {
        Console.WriteLine($"Dry-run: would replay request {request.Id}");
        Console.WriteLine($"  Method:   {request.HttpMethod}");
        Console.WriteLine($"  URL:      {request.Url}");
        Console.WriteLine($"  Captured: {request.Timestamp:u}");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization", "CA1303:Do not pass literals as localized parameters",
        Justification = "CLI output strings are intentionally not localized.")]
    private static void PrintReplayResult(CapturedRequest request, ReplayResult result)
    {
        Console.WriteLine($"Request:  {request.Id}");
        Console.WriteLine($"Method:   {request.HttpMethod}");
        Console.WriteLine($"URL:      {request.Url}");
        Console.WriteLine($"Status:   {result.ResponseStatus}  (original: {request.ResponseStatus})");
        Console.WriteLine($"Duration: {result.DurationMs} ms");

        var diffCount = result.Diffs.Count;
        Console.WriteLine($"Diffs ({diffCount}):");
        if (diffCount == 0)
        {
            Console.WriteLine("  (none - responses match)");
        }
        else
        {
            foreach (var diff in result.Diffs)
            {
                Console.WriteLine($"  {diff.Path}");
                Console.WriteLine($"    expected: {diff.Expected ?? "(null)"}");
                Console.WriteLine($"    actual:   {diff.Actual   ?? "(null)"}");
            }
        }
    }
}
