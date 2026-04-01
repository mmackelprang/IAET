using System.CommandLine;
using Iaet.Explorer;

namespace Iaet.Cli.Commands;

internal static class ExploreCommand
{
    internal static Command Create()
    {
        var exploreCmd = new Command("explore", "Start the IAET Explorer web UI");

        var dbOption       = new Option<string>("--db")       { Description = "Path to the SQLite catalog database", Required = true };
        var portOption     = new Option<int>("--port")         { Description = "Port to listen on (default: 9200)", DefaultValueFactory = _ => 9200 };
        var projectsOption = new Option<string>("--projects")  { Description = "Path to projects directory", DefaultValueFactory = _ => Path.Combine(Directory.GetCurrentDirectory(), ".iaet-projects") };

        exploreCmd.Add(dbOption);
        exploreCmd.Add(portOption);
        exploreCmd.Add(projectsOption);

        exploreCmd.SetAction(async (parseResult) =>
        {
            var db           = parseResult.GetRequiredValue(dbOption);
            var port         = parseResult.GetValue(portOption);
            var projectsRoot = parseResult.GetValue(projectsOption) ?? Path.Combine(Directory.GetCurrentDirectory(), ".iaet-projects");

            if (!File.Exists(db))
            {
                await Console.Error.WriteLineAsync($"Error: database file not found: {db}").ConfigureAwait(false);
                return;
            }

            var app = ExplorerApp.Build(db, port, projectsRoot);

            Console.WriteLine($"IAET Explorer running at http://localhost:{port}");
            Console.WriteLine($"Dashboard: http://localhost:{port}/dashboard");
            Console.WriteLine("Press Ctrl+C to stop.");

            await app.RunAsync().ConfigureAwait(false);
        });

        return exploreCmd;
    }
}
