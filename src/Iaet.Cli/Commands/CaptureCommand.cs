using System.CommandLine;
using Iaet.Capture;
using Iaet.Capture.Listeners;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Crawler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class CaptureCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var captureCmd = new Command("capture", "Manage capture sessions");

        var startCmd = new Command("start", "Start a new capture session");

        var targetOption = new Option<string>("--target") { Description = "Target application name", Required = true };
        var profileOption = new Option<string>("--profile") { Description = "Browser profile name", DefaultValueFactory = _ => "default" };
        var urlOption = new Option<string>("--url") { Description = "Starting URL", Required = true };
        var sessionOption = new Option<string>("--session") { Description = "Session name", Required = true };
        var headlessOption = new Option<bool>("--headless") { Description = "Run browser in headless mode" };
        var captureStreamsOption = new Option<bool>("--capture-streams") { Description = "Enable stream capture (WebSocket, SSE, WebRTC, HLS/DASH, gRPC-Web)", DefaultValueFactory = _ => true };
        var captureSamplesOption = new Option<bool>("--capture-samples") { Description = "Capture sample payload frames", DefaultValueFactory = _ => false };
        var captureDurationOption = new Option<int>("--capture-duration") { Description = "Sample capture duration in seconds", DefaultValueFactory = _ => 10 };
        var captureFramesOption = new Option<int>("--capture-frames") { Description = "Maximum frames to capture per connection", DefaultValueFactory = _ => 1000 };

        startCmd.Add(targetOption);
        startCmd.Add(profileOption);
        startCmd.Add(urlOption);
        startCmd.Add(sessionOption);
        startCmd.Add(headlessOption);
        startCmd.Add(captureStreamsOption);
        startCmd.Add(captureSamplesOption);
        startCmd.Add(captureDurationOption);
        startCmd.Add(captureFramesOption);

        startCmd.SetAction(async (parseResult) =>
        {
            var target = parseResult.GetRequiredValue(targetOption);
            var profile = parseResult.GetValue(profileOption)!;
            var url = parseResult.GetRequiredValue(urlOption);
            var sessionName = parseResult.GetRequiredValue(sessionOption);
            var headless = parseResult.GetValue(headlessOption);
            var captureStreams = parseResult.GetValue(captureStreamsOption);
            var captureSamples = parseResult.GetValue(captureSamplesOption);
            var captureDuration = parseResult.GetValue(captureDurationOption);
            var captureFrames = parseResult.GetValue(captureFramesOption);

            Console.WriteLine($"Starting capture session '{sessionName}' for {target}...");
            Console.WriteLine("Browser will open. Perform actions, then press Enter to stop.");

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var factory = scope.ServiceProvider.GetRequiredService<ICaptureSessionFactory>();

            var streamOptions = new StreamCaptureOptions
            {
                Enabled = captureStreams,
                CaptureSamples = captureSamples,
                SampleDurationSeconds = captureDuration,
                MaxFramesPerConnection = captureFrames
            };

            IStreamCatalog? streamCatalog = null;
            IReadOnlyList<IProtocolListener>? listeners = null;

            if (captureStreams)
            {
                streamCatalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
                listeners =
                [
                    new WebSocketListener(streamOptions),
                    new SseListener(streamOptions),
                    new MediaStreamListener(streamOptions),
                    new GrpcWebListener(streamOptions),
                    new WebRtcListener(streamOptions)
                ];
                Console.WriteLine("Stream capture enabled (WebSocket, SSE, WebRTC, HLS/DASH, gRPC-Web).");
            }

            var session = factory.Create(new CaptureOptions
            {
                TargetApplication = target,
                Profile = profile,
                Headless = headless,
                Streams = streamOptions
            }, streamCatalog, listeners);

            await using (session.ConfigureAwait(false))
            {
                var sessionInfo = new Iaet.Core.Models.CaptureSessionInfo
                {
                    Id = session.SessionId,
                    Name = sessionName,
                    TargetApplication = target,
                    Profile = profile,
                    StartedAt = DateTimeOffset.UtcNow
                };
                await catalog.SaveSessionAsync(sessionInfo).ConfigureAwait(false);

                await session.StartAsync(url).ConfigureAwait(false);
                Console.WriteLine($"Recording... Session ID: {session.SessionId}");
                Console.ReadLine();

                var count = 0;
                await foreach (var request in session.GetCapturedRequestsAsync().ConfigureAwait(false))
                {
                    await catalog.SaveRequestAsync(request).ConfigureAwait(false);
                    count++;
                }

                await session.StopAsync().ConfigureAwait(false);
                Console.WriteLine($"Captured {count} requests.");
            }
        });

        captureCmd.Add(startCmd);
        captureCmd.Add(CreateRunCmd(services));
        return captureCmd;
    }

    private static Command CreateRunCmd(IServiceProvider services)
    {
        var runCmd       = new Command("run", "Execute a TypeScript Playwright recipe against a running browser session");
        var recipeOption  = new Option<string>("--recipe")  { Description = "Path to the TypeScript recipe file (.ts)", Required = true };
        var sessionOption = new Option<string>("--session") { Description = "Session name for reference", Required = true };

        runCmd.Add(recipeOption);
        runCmd.Add(sessionOption);

        runCmd.SetAction(async (parseResult) =>
        {
            var recipePath  = parseResult.GetRequiredValue(recipeOption);
            var sessionName = parseResult.GetRequiredValue(sessionOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);

            // Validate recipe path
            RecipeRunner.ValidateRecipe(recipePath);

            var (command, args) = RecipeRunner.BuildCommand(recipePath, 9222);
            var env             = RecipeRunner.GetEnvironment(9222);

            Console.WriteLine($"Session:  {sessionName}");
            Console.WriteLine($"Recipe:   {recipePath}");
            Console.WriteLine($"Command:  {command} {args}");
            Console.WriteLine($"Env:      CDP_ENDPOINT={env["CDP_ENDPOINT"]}");
            Console.WriteLine();
            Console.WriteLine("NOTE: Full CDP integration requires a running browser with remote-debugging enabled.");
            Console.WriteLine("      Launch Chrome with --remote-debugging-port=9222, then run this command.");
            Console.WriteLine("      The recipe will connect via CDP_ENDPOINT and drive the browser.");
        });

        return runCmd;
    }
}
