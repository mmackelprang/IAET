using System.CommandLine;
using Iaet.Capture;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
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

        startCmd.Add(targetOption);
        startCmd.Add(profileOption);
        startCmd.Add(urlOption);
        startCmd.Add(sessionOption);
        startCmd.Add(headlessOption);

        startCmd.SetAction(async (parseResult) =>
        {
            var target = parseResult.GetRequiredValue(targetOption);
            var profile = parseResult.GetValue(profileOption)!;
            var url = parseResult.GetRequiredValue(urlOption);
            var sessionName = parseResult.GetRequiredValue(sessionOption);
            var headless = parseResult.GetValue(headlessOption);

            Console.WriteLine($"Starting capture session '{sessionName}' for {target}...");
            Console.WriteLine("Browser will open. Perform actions, then press Enter to stop.");

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var factory = scope.ServiceProvider.GetRequiredService<ICaptureSessionFactory>();

            var session = factory.Create(new CaptureOptions
            {
                TargetApplication = target,
                Profile = profile,
                Headless = headless
            });
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
        return captureCmd;
    }
}
