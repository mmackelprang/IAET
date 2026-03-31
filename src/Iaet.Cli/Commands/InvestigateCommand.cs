// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Capture;
using Iaet.Capture.Listeners;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.Export;
using Iaet.Export.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

/// <summary>
/// <c>iaet investigate</c> — a guided interactive wizard that walks users through the
/// full IAET workflow: capture → analyze → document.
/// </summary>
/// <remarks>
/// The wizard is an orchestrator: it calls existing services (IEndpointCatalog,
/// IStreamCatalog, ISchemaInferrer, ExportContext, etc.) and does NOT re-implement logic.
/// </remarks>
internal static class InvestigateCommand
{
    // ── Capture method labels ───────────────────────────────────────────────

    private static readonly IReadOnlyList<string> CaptureMethodOptions =
    [
        "Interactive (you browse, IAET captures)",
        "Automated crawler (IAET explores on its own)",
        "Import from file (.iaet.json)",
    ];

    // ── What-next menu labels ───────────────────────────────────────────────

    private static readonly IReadOnlyList<string> WhatNextOptions =
    [
        "View discovered endpoints",
        "View discovered streams",
        "Infer schemas (runs for all endpoints with response bodies)",
        "Export as OpenAPI YAML",
        "Export as Markdown report",
        "Export as HTML report",
        "Open interactive explorer",
        "Start another capture session",
        "Exit",
    ];

    // ── Command factory ─────────────────────────────────────────────────────

    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("investigate", "Guided interactive wizard: capture → analyze → document");

        var projectOption = new Option<string?>("--project") { Description = "Investigation project name (uses agent-based workflow)" };
        cmd.Add(projectOption);

        cmd.SetAction(async (parseResult) =>
        {
            var projectName = parseResult.GetValue(projectOption);
            if (projectName is not null)
            {
                await RunProjectInvestigationAsync(services, projectName).ConfigureAwait(false);
            }
            else
            {
                await RunWizardAsync(services).ConfigureAwait(false);
            }
        });

        return cmd;
    }

    // ── Project-based investigation ─────────────────────────────────────────

    private static async Task RunProjectInvestigationAsync(IServiceProvider services, string projectName)
    {
        using var scope = services.CreateScope();
        var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();

        var config = await projectStore.LoadAsync(projectName).ConfigureAwait(false);
        if (config is null)
        {
            Console.WriteLine($"Project '{projectName}' not found.");
            Console.WriteLine($"Create it first: iaet project create --name {projectName} --url <target-url>");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     IAET — Agent Investigation Team                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"  Project:  {config.DisplayName}");
        Console.WriteLine($"  Target:   {config.EntryPoints[0].Url}");
        Console.WriteLine($"  Type:     {config.TargetType}");
        Console.WriteLine($"  Auth:     {(config.AuthRequired ? "required" : "none")}");
        Console.WriteLine($"  Status:   {config.Status}");
        Console.WriteLine($"  Rounds:   {config.CurrentRound}");
        Console.WriteLine($"  Dir:      {projectStore.GetProjectDirectory(projectName)}");
        Console.WriteLine();

        if (config.Status == ProjectStatus.New)
        {
            var updated = config with { Status = ProjectStatus.Investigating };
            await projectStore.SaveAsync(updated).ConfigureAwait(false);
        }

        Console.WriteLine("  To begin the investigation, tell Claude Code:");
        Console.WriteLine();
        Console.WriteLine($"    \"Investigate the project {projectName} following the Lead");
        Console.WriteLine("     Investigator protocol in agents/lead-investigator.md\"");
        Console.WriteLine();
        Console.WriteLine("  The Lead Investigator will:");
        Console.WriteLine("    1. Assess the target and plan the first round");
        Console.WriteLine("    2. Ask you to log in if auth is required");
        Console.WriteLine("    3. Dispatch specialist agents (capture, cookies, crawler)");
        Console.WriteLine("    4. Analyze findings and decide: another round or finalize");
        Console.WriteLine();
        Console.WriteLine("  Available agents: agents/*.md");
        Console.WriteLine("  Project data:     .iaet-projects/" + projectName + "/");
        Console.WriteLine();
    }

    // ── Main wizard loop ────────────────────────────────────────────────────

    private static async Task RunWizardAsync(IServiceProvider services)
    {
        WizardPrompt.PrintBanner();

        // Ensure DB schema is up to date before anything else
        using var setupScope = services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await setupDb.Database.MigrateAsync().ConfigureAwait(false);

        // Gather target + session information once
        var targetName = WizardPrompt.ReadString("Target application name");
        var startingUrl = WizardPrompt.ReadString("Starting URL");

        var defaultSession = WizardPrompt.GenerateSessionName(targetName);
        var sessionName = WizardPrompt.ReadString("Session name", defaultSession);

        Guid? activeSessionId = null;

        while (true)
        {
            // ── Capture step ────────────────────────────────────────────────
            activeSessionId = await RunCaptureStepAsync(
                services, targetName, startingUrl, sessionName, activeSessionId)
                .ConfigureAwait(false);

            if (activeSessionId is null)
            {
                // User chose a path (crawler / import) that produced no persistent session ID
                // through the wizard (those subcommands manage their own session IDs).
                Console.WriteLine();
                Console.WriteLine("(No session ID available from this capture method in wizard mode.)");
                Console.WriteLine("Use 'iaet catalog sessions' to list available sessions, then re-run 'iaet investigate'.");
                return;
            }

            // ── Summary step ────────────────────────────────────────────────
            await PrintSummaryAsync(services, activeSessionId.Value).ConfigureAwait(false);

            // ── What next? loop ─────────────────────────────────────────────
            var continueOuter = await RunWhatNextLoopAsync(
                services, activeSessionId.Value, targetName, startingUrl).ConfigureAwait(false);

            if (!continueOuter)
                break;

            // User chose "Start another capture session" — prompt for new session name
            sessionName = WizardPrompt.ReadString(
                "New session name",
                WizardPrompt.GenerateSessionName(targetName));
        }

        Console.WriteLine();
        Console.WriteLine("Investigation complete. Goodbye!");
        Console.WriteLine();
    }

    // ── Capture step ─────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the capture step and returns the session ID if one was created,
    /// or <see langword="null"/> when the wizard cannot track the session.
    /// </summary>
    private static async Task<Guid?> RunCaptureStepAsync(
        IServiceProvider services,
        string targetName,
        string startingUrl,
        string sessionName,
        Guid? existingSessionId)
    {
        Console.WriteLine();
        var methodIndex = WizardPrompt.ReadChoice("Capture method", CaptureMethodOptions);

        return methodIndex switch
        {
            0 => await RunInteractiveCaptureAsync(
                     services, targetName, startingUrl, sessionName, existingSessionId)
                     .ConfigureAwait(false),
            1 => RunCrawlerCapture(targetName, startingUrl, sessionName),
            2 => RunImportCapture(),
            _ => null,
        };
    }

    // ── Interactive capture (option 1) ────────────────────────────────────────

    private static async Task<Guid?> RunInteractiveCaptureAsync(
        IServiceProvider services,
        string targetName,
        string startingUrl,
        string sessionName,
        Guid? existingSessionId)
    {
        Console.WriteLine();
        Console.WriteLine($"Starting capture session '{sessionName}'...");
        Console.WriteLine("Browser will open. Perform actions, then press Enter to stop.");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var factory = scope.ServiceProvider.GetRequiredService<ICaptureSessionFactory>();

        var streamCatalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
        var streamOptions = new StreamCaptureOptions { Enabled = true };

        IReadOnlyList<IProtocolListener> listeners =
        [
            new WebSocketListener(streamOptions),
            new SseListener(streamOptions),
            new MediaStreamListener(streamOptions),
            new GrpcWebListener(streamOptions),
            new WebRtcListener(streamOptions),
        ];

        var session = factory.Create(
            new CaptureOptions
            {
                TargetApplication = targetName,
                Profile = "default",
                Headless = false,
                Streams = streamOptions,
            },
            streamCatalog,
            listeners);

        var sessionId = existingSessionId ?? session.SessionId;

        await using (session.ConfigureAwait(false))
        {
            var sessionInfo = new CaptureSessionInfo
            {
                Id = session.SessionId,
                Name = sessionName,
                TargetApplication = targetName,
                Profile = "default",
                StartedAt = DateTimeOffset.UtcNow,
            };
            await catalog.SaveSessionAsync(sessionInfo).ConfigureAwait(false);

            await session.StartAsync(startingUrl).ConfigureAwait(false);
            Console.WriteLine($"Recording... (Session ID: {session.SessionId})");
            Console.ReadLine();

            var count = 0;
            await foreach (var request in session.GetCapturedRequestsAsync().ConfigureAwait(false))
            {
                await catalog.SaveRequestAsync(request).ConfigureAwait(false);
                count++;
            }

            await session.StopAsync().ConfigureAwait(false);
            Console.WriteLine($"Captured {count} requests.");

            return session.SessionId;
        }
    }

    // ── Crawler capture (option 2) ─────────────────────────────────────────────

    private static Guid? RunCrawlerCapture(string targetName, string startingUrl, string sessionName)
    {
        Console.WriteLine();
        Console.WriteLine($"Crawler mode selected for target '{targetName}'.");
        Console.WriteLine($"  Start URL:   {startingUrl}");
        Console.WriteLine($"  Session:     {sessionName}");
        Console.WriteLine();
        Console.WriteLine("To run the crawler, use the dedicated command:");
        Console.WriteLine($"  iaet crawl --url \"{startingUrl}\" --target \"{targetName}\" --session \"{sessionName}\"");
        Console.WriteLine();
        Console.WriteLine("(The crawler requires a live Playwright browser instance and is not available");
        Console.WriteLine(" within the wizard. After running, use 'iaet investigate' to analyze results.)");
        return null;
    }

    // ── Import capture (option 3) ──────────────────────────────────────────────

    private static Guid? RunImportCapture()
    {
        Console.WriteLine();
        Console.WriteLine("To import a .iaet.json file captured by the browser extension, use:");
        Console.WriteLine("  iaet import --file <path-to-file.iaet.json>");
        Console.WriteLine();
        Console.WriteLine("After importing, use 'iaet catalog sessions' to find the session ID,");
        Console.WriteLine("then re-run 'iaet investigate' to analyze the imported data.");
        return null;
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    private static async Task PrintSummaryAsync(IServiceProvider services, Guid sessionId)
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var streamCatalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();

        var requests = await catalog.GetRequestsBySessionAsync(sessionId).ConfigureAwait(false);
        var groups = await catalog.GetEndpointGroupsAsync(sessionId).ConfigureAwait(false);
        var streams = await streamCatalog.GetStreamsBySessionAsync(sessionId).ConfigureAwait(false);

        Console.WriteLine();
        WizardPrompt.PrintDivider();
        Console.WriteLine($"  Session summary");
        Console.WriteLine($"  Requests:  {requests.Count}");
        Console.WriteLine($"  Endpoints: {groups.Count} unique");
        Console.WriteLine($"  Streams:   {streams.Count}");
        WizardPrompt.PrintDivider();
        Console.WriteLine();
    }

    // ── What next? loop ───────────────────────────────────────────────────────

    /// <summary>
    /// Loops the "What next?" menu until the user exits or requests another capture session.
    /// Returns <see langword="true"/> when the user chooses to start another capture session,
    /// <see langword="false"/> to exit.
    /// </summary>
    private static async Task<bool> RunWhatNextLoopAsync(
        IServiceProvider services,
        Guid sessionId,
        string targetName,
        string startingUrl)
    {
        while (true)
        {
            Console.WriteLine();
            var choice = WizardPrompt.ReadChoice("What next?", WhatNextOptions);

            switch (choice)
            {
                case 0: // View discovered endpoints
                    await ShowEndpointsAsync(services, sessionId).ConfigureAwait(false);
                    break;

                case 1: // View discovered streams
                    await ShowStreamsAsync(services, sessionId).ConfigureAwait(false);
                    break;

                case 2: // Infer schemas
                    await InferSchemasAsync(services, sessionId).ConfigureAwait(false);
                    break;

                case 3: // Export as OpenAPI
                    await ExportFileAsync(
                        services, sessionId,
                        ctx => OpenApiGenerator.Generate(ctx),
                        targetName, "openapi", "yaml", "OpenAPI YAML")
                        .ConfigureAwait(false);
                    break;

                case 4: // Export as Markdown report
                    await ExportFileAsync(
                        services, sessionId,
                        ctx => MarkdownReportGenerator.Generate(ctx),
                        targetName, "report", "md", "Markdown report")
                        .ConfigureAwait(false);
                    break;

                case 5: // Export as HTML report
                    await ExportFileAsync(
                        services, sessionId,
                        ctx => HtmlReportGenerator.Generate(ctx),
                        targetName, "report", "html", "HTML report")
                        .ConfigureAwait(false);
                    break;

                case 6: // Open interactive explorer
                    LaunchExplorer(targetName, startingUrl);
                    break;

                case 7: // Start another capture session
                    return true; // caller will re-enter capture step

                case 8: // Exit
                    return false;

                default:
                    Console.WriteLine("Unknown option.");
                    break;
            }
        }
    }

    // ── Menu actions ──────────────────────────────────────────────────────────

    private static async Task ShowEndpointsAsync(IServiceProvider services, Guid sessionId)
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var groups = await catalog.GetEndpointGroupsAsync(sessionId).ConfigureAwait(false);

        Console.WriteLine();
        if (groups.Count == 0)
        {
            Console.WriteLine("  No endpoints discovered yet.");
            return;
        }

        Console.WriteLine($"  {"Endpoint",-52} {"Count",-8} {"Last Seen"}");
        WizardPrompt.PrintDivider(80);
        foreach (var g in groups)
        {
            Console.WriteLine(
                $"  {g.Signature.Normalized,-52} {g.ObservationCount,-8} {g.LastSeen:g}");
        }
        WizardPrompt.PrintDivider(80);
        Console.WriteLine($"  {groups.Count} endpoint(s) total.");
    }

    private static async Task ShowStreamsAsync(IServiceProvider services, Guid sessionId)
    {
        using var scope = services.CreateScope();
        var streamCatalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
        var streams = await streamCatalog.GetStreamsBySessionAsync(sessionId).ConfigureAwait(false);

        Console.WriteLine();
        if (streams.Count == 0)
        {
            Console.WriteLine("  No streams captured.");
            return;
        }

        Console.WriteLine($"  {"Protocol",-20} {"URL",-50} {"Started"}");
        WizardPrompt.PrintDivider(80);
        foreach (var s in streams)
        {
            Console.WriteLine($"  {s.Protocol,-20} {s.Url,-50} {s.StartedAt:g}");
        }
        WizardPrompt.PrintDivider(80);
        Console.WriteLine($"  {streams.Count} stream(s) total.");
    }

    private static async Task InferSchemasAsync(IServiceProvider services, Guid sessionId)
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var inferrer = scope.ServiceProvider.GetRequiredService<ISchemaInferrer>();

        var groups = await catalog.GetEndpointGroupsAsync(sessionId).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine("  Inferring schemas...");

        var inferred = 0;
        var skipped = 0;

        foreach (var group in groups)
        {
            var bodies = await catalog.GetResponseBodiesAsync(
                sessionId, group.Signature.Normalized).ConfigureAwait(false);

            if (bodies.Count == 0)
            {
                skipped++;
                continue;
            }

            var result = await inferrer.InferAsync(bodies).ConfigureAwait(false);
            inferred++;

            Console.WriteLine();
            Console.WriteLine($"  [{group.Signature.Normalized}]");
            Console.WriteLine($"  JSON Schema: {result.JsonSchema[..Math.Min(result.JsonSchema.Length, 80)]}...");

            if (result.Warnings.Count > 0)
            {
                foreach (var w in result.Warnings)
                    Console.WriteLine($"    ! {w}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  Schemas inferred: {inferred}  |  Skipped (no bodies): {skipped}");
    }

    private static async Task ExportFileAsync(
        IServiceProvider services,
        Guid sessionId,
        Func<ExportContext, string> generator,
        string targetName,
        string filePrefix,
        string extension,
        string formatName)
    {
        var slug = WizardPrompt.SlugifyTarget(targetName);
        var defaultPath = $"{slug}-{filePrefix}.{extension}";
        var outputPath = WizardPrompt.ReadString($"Output file path", defaultPath);

        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var streamCatalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
        var schemaInferrer = scope.ServiceProvider.GetRequiredService<ISchemaInferrer>();

        Console.WriteLine($"  Building {formatName}...");

        var ctx = await ExportContext.LoadAsync(
            sessionId, catalog, streamCatalog, schemaInferrer).ConfigureAwait(false);

        var output = generator(ctx);
        await File.WriteAllTextAsync(outputPath, output).ConfigureAwait(false);

        Console.WriteLine($"  {formatName} written to: {outputPath}");
    }

    private static void LaunchExplorer(string targetName, string startingUrl)
    {
        Console.WriteLine();
        Console.WriteLine("  To launch the Explorer web UI, run:");
        Console.WriteLine("    iaet explore --db catalog.db");
        Console.WriteLine();
        Console.WriteLine($"  Target:  {targetName}");
        Console.WriteLine($"  URL:     {startingUrl}");
        Console.WriteLine("  The Explorer provides a Swagger-like interface at http://localhost:9200");
    }
}
