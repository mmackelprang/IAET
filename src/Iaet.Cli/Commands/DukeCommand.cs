// src/Iaet.Cli/Commands/DukeCommand.cs
// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Iaet.DukeEnergy;
using Iaet.DukeEnergy.Abstractions;
using Iaet.DukeEnergy.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

/// <summary>
/// Commands for the Duke Energy outage REST service: run it, or query it directly from the shell.
/// </summary>
internal static class DukeCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    internal static Command Create()
    {
        var cmd = new Command("duke", "Duke Energy outage REST service and queries");
        cmd.Add(CreateServeCmd());
        cmd.Add(CreateStatusCmd());
        cmd.Add(CreateNeighborhoodCmd());
        return cmd;
    }

    private static Option<string?> SettingsOption() => new("--settings")
    {
        Description = "Path to a JSON settings file containing a \"DukeEnergy\" section",
    };

    private static Command CreateServeCmd()
    {
        var serveCmd = new Command("serve", "Start the Duke Energy outage REST service");

        var portOption     = new Option<int>("--port") { Description = "Port to listen on (default: 9300)", DefaultValueFactory = _ => 9300 };
        var settingsOption = SettingsOption();
        var allOption      = new Option<bool>("--all-interfaces") { Description = "Bind all interfaces instead of loopback only" };

        serveCmd.Add(portOption);
        serveCmd.Add(settingsOption);
        serveCmd.Add(allOption);

        serveCmd.SetAction(async (parseResult) =>
        {
            var settings = parseResult.GetValue(settingsOption);

            if (!string.IsNullOrWhiteSpace(settings) && !File.Exists(settings))
            {
                await Console.Error.WriteLineAsync($"Error: settings file not found: {settings}").ConfigureAwait(false);
                return;
            }

            var port = parseResult.GetValue(portOption);

            var app = DukeEnergyApiApp.Build(new DukeEnergyApiOptions
            {
                Port                  = port,
                SettingsPath          = settings,
                ListenOnAllInterfaces = parseResult.GetValue(allOption),
            });

            Console.WriteLine($"Duke Energy outage API running at http://localhost:{port}");
            Console.WriteLine($"  Neighborhood: http://localhost:{port}/api/v1/outages/neighborhood?lat=35.78&lon=-78.64&radiusMiles=1");
            Console.WriteLine($"  Home status:  http://localhost:{port}/api/v1/home/status");
            Console.WriteLine("Press Ctrl+C to stop.");

            await app.RunAsync().ConfigureAwait(false);
        });

        return serveCmd;
    }

    private static Command CreateStatusCmd()
    {
        var statusCmd = new Command("status", "Print the outage status for the configured home");
        var settingsOption = SettingsOption();
        statusCmd.Add(settingsOption);

        statusCmd.SetAction(async (parseResult) =>
        {
            using var provider = BuildProvider(parseResult.GetValue(settingsOption));

            var service = provider.GetRequiredService<IHomeOutageService>();
            var status  = await service.GetHomeStatusAsync().ConfigureAwait(false);

            Console.WriteLine(JsonSerializer.Serialize(status, JsonOptions));
        });

        return statusCmd;
    }

    private static Command CreateNeighborhoodCmd()
    {
        var cmd = new Command("neighborhood", "List outages near a point");

        var latOption          = new Option<double>("--lat")    { Description = "Latitude in degrees", Required = true };
        var lonOption          = new Option<double>("--lon")    { Description = "Longitude in degrees", Required = true };
        var radiusOption       = new Option<double>("--radius") { Description = "Radius in miles (default: 1)", DefaultValueFactory = _ => 1.0 };
        var jurisdictionOption = new Option<string?>("--jurisdiction") { Description = "Operating-company code, e.g. DEC, DEF, DEI, DEM" };
        var settingsOption     = SettingsOption();

        cmd.Add(latOption);
        cmd.Add(lonOption);
        cmd.Add(radiusOption);
        cmd.Add(jurisdictionOption);
        cmd.Add(settingsOption);

        cmd.SetAction(async (parseResult) =>
        {
            using var provider = BuildProvider(parseResult.GetValue(settingsOption));

            var client = provider.GetRequiredService<IOutageMapClient>();
            var report = await client
                .GetNeighborhoodAsync(
                    parseResult.GetRequiredValue(latOption),
                    parseResult.GetRequiredValue(lonOption),
                    parseResult.GetValue(radiusOption),
                    parseResult.GetValue(jurisdictionOption))
                .ConfigureAwait(false);

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{report.OutageCount} outage(s) within {report.RadiusMiles} mile(s); {report.CustomersAffected} customer(s) affected."));
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        });

        return cmd;
    }

    /// <summary>
    /// Builds a standalone provider for the query commands. The CLI's shared host does not register
    /// the Duke Energy client, because its configuration is per-invocation.
    /// </summary>
    private static ServiceProvider BuildProvider(string? settingsPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(settingsPath ?? "dukeenergy.settings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddDukeEnergy(configuration.GetSection("DukeEnergy").Bind);

        return services.BuildServiceProvider();
    }
}
