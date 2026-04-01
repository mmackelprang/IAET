// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.JsAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

/// <summary>
/// <c>iaet analyze</c> — post-capture analysis commands.
/// Currently exposes the <c>correlate</c> sub-command which runs the
/// <see cref="CrossEndpointCorrelator"/> over a session and persists the
/// results to the project knowledge base.
/// </summary>
internal static class AnalyzeCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    internal static Command Create(IServiceProvider services)
    {
        var analyzeCmd = new Command("analyze", "Post-capture analysis (cross-endpoint correlation, etc.)");
        analyzeCmd.Add(CreateCorrelateCmd(services));
        return analyzeCmd;
    }

    // ── iaet analyze correlate ──────────────────────────────────────────────

    private static Command CreateCorrelateCmd(IServiceProvider services)
    {
        var cmd = new Command("correlate",
            "Trace values across endpoints and streams to resolve protojson field names");

        var projectOption = new Option<string>("--project")
        {
            Description = "Project name (knowledge/correlations.json will be written here)",
            Required = true,
        };
        var sessionIdOption = new Option<Guid>("--session-id")
        {
            Description = "Session ID to correlate",
            Required = true,
        };

        cmd.Add(projectOption);
        cmd.Add(sessionIdOption);

        cmd.SetAction(async (parseResult) =>
        {
            var projectName = parseResult.GetRequiredValue(projectOption);
            var sessionId   = parseResult.GetRequiredValue(sessionIdOption);

            await RunCorrelateAsync(services, projectName, sessionId).ConfigureAwait(false);
        });

        return cmd;
    }

    private static async Task RunCorrelateAsync(
        IServiceProvider services,
        string projectName,
        Guid sessionId)
    {
        using var scope = services.CreateScope();

        // Migrate DB
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        // Load project
        var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
        var config = await projectStore.LoadAsync(projectName).ConfigureAwait(false);
        if (config is null)
        {
            await Console.Error.WriteLineAsync($"Project '{projectName}' not found.").ConfigureAwait(false);
            return;
        }

        // Load data
        var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var streamCatalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();

        Console.WriteLine($"Loading session {sessionId}...");
        var requests = await catalog.GetRequestsBySessionAsync(sessionId).ConfigureAwait(false);
        var streams  = await streamCatalog.GetStreamsBySessionAsync(sessionId).ConfigureAwait(false);

        Console.WriteLine($"  Requests: {requests.Count}");
        Console.WriteLine($"  Streams:  {streams.Count}");

        if (requests.Count == 0)
        {
            Console.WriteLine("No requests found for this session. Nothing to correlate.");
            return;
        }

        // Run correlators
        Console.WriteLine("Running cross-endpoint correlator...");
        var requestCorrelations = CrossEndpointCorrelator.Correlate(requests);
        Console.WriteLine($"  Request correlations: {requestCorrelations.Count}");

        IReadOnlyList<ValueCorrelation> streamCorrelations = [];
        if (streams.Count > 0)
        {
            Console.WriteLine("Running stream correlator...");
            streamCorrelations = CrossEndpointCorrelator.CorrelateWithStreams(requests, streams);
            Console.WriteLine($"  Stream correlations: {streamCorrelations.Count}");
        }

        // Merge all correlations (request + stream, deduplicated by source:position)
        var all = MergeCorrelations(requestCorrelations, streamCorrelations);
        Console.WriteLine($"  Total unique correlations: {all.Count}");

        // Write to knowledge/correlations.json
        var projectDir = projectStore.GetProjectDirectory(projectName);
        var knowledgeDir = Path.Combine(projectDir, "knowledge");
        Directory.CreateDirectory(knowledgeDir);
        var outputPath = Path.Combine(knowledgeDir, "correlations.json");

#pragma warning disable CA1308 // Confidence rendered lowercase in JSON for readability
        var output = new
        {
            sessionId,
            generatedAt = DateTimeOffset.UtcNow,
            totalCorrelations = all.Count,
            correlations = all.Select(c => new
            {
                value = c.Value,
                sourceEndpoint = c.SourceEndpoint,
                sourcePosition = c.SourcePosition,
                consumedBy = c.ConsumedBy,
                consumedContext = c.ConsumedContext,
                suggestedName = c.SuggestedName,
                confidence = c.Confidence.ToString().ToLowerInvariant(),
            }).ToList(),
        };
#pragma warning restore CA1308

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(output, JsonOptions))
            .ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Correlations written to: {outputPath}");

        if (all.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Top correlations:");
            foreach (var c in all.Take(10))
            {
                Console.WriteLine($"  [{c.SourceEndpoint}] position {c.SourcePosition}");
                Console.WriteLine($"    -> {c.SuggestedName}  ({c.ConsumedContext}, {c.Confidence} confidence)");
            }

            if (all.Count > 10)
                Console.WriteLine($"  ... and {all.Count - 10} more (see {outputPath})");
        }
    }

    /// <summary>
    /// Merges request and stream correlations, keeping the highest-confidence entry
    /// when the same source endpoint + position appears in both sets.
    /// </summary>
    private static List<ValueCorrelation> MergeCorrelations(
        IReadOnlyList<ValueCorrelation> requestCorrelations,
        IReadOnlyList<ValueCorrelation> streamCorrelations)
    {
        var merged = new Dictionary<string, ValueCorrelation>(StringComparer.Ordinal);

        foreach (var c in requestCorrelations.Concat(streamCorrelations))
        {
            var key = $"{c.SourceEndpoint}:{c.SourcePosition}";
            if (!merged.TryGetValue(key, out var existing) ||
                c.Confidence < existing.Confidence) // High=0 is best
            {
                merged[key] = c;
            }
        }

        return merged.Values
            .OrderBy(c => c.SourceEndpoint, StringComparer.Ordinal)
            .ThenBy(c => c.SourcePosition)
            .ToList();
    }
}
