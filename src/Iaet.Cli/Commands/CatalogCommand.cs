using System.CommandLine;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class CatalogCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var catalogCmd = new Command("catalog", "Browse the endpoint catalog");

        var listCmd = new Command("sessions", "List capture sessions");
        listCmd.SetAction(async (_) =>
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var sessions = await catalog.ListSessionsAsync().ConfigureAwait(false);

            if (sessions.Count == 0)
            {
                Console.WriteLine("No sessions found.");
                return;
            }

            Console.WriteLine($"{"ID",-38} {"Name",-20} {"Target",-20} {"Requests",-10} {"Started"}");
            Console.WriteLine(new string('-', 110));
            foreach (var s in sessions)
            {
                Console.WriteLine($"{s.Id,-38} {s.Name,-20} {s.TargetApplication,-20} {s.CapturedRequestCount,-10} {s.StartedAt:g}");
            }
        });

        var endpointsCmd = new Command("endpoints", "List discovered endpoints");
        var sessionIdOption = new Option<Guid>("--session-id") { Description = "Session ID", Required = true };
        endpointsCmd.Add(sessionIdOption);
        endpointsCmd.SetAction(async (parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionIdOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var groups = await catalog.GetEndpointGroupsAsync(sessionId).ConfigureAwait(false);

            if (groups.Count == 0)
            {
                Console.WriteLine("No endpoints found.");
                return;
            }

            Console.WriteLine($"{"Endpoint",-50} {"Count",-8} {"First Seen",-22} {"Last Seen"}");
            Console.WriteLine(new string('-', 105));
            foreach (var g in groups)
            {
                Console.WriteLine($"{g.Signature.Normalized,-50} {g.ObservationCount,-8} {g.FirstSeen:g,-22} {g.LastSeen:g}");
            }
        });

        catalogCmd.Add(listCmd);
        catalogCmd.Add(endpointsCmd);
        return catalogCmd;
    }
}
