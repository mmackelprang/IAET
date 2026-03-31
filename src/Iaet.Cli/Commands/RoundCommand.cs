// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class RoundCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("round", "Manage investigation rounds");
        cmd.Add(CreateStatusCmd(services));
        return cmd;
    }

    private static Command CreateStatusCmd(IServiceProvider services)
    {
        var statusCmd = new Command("status", "Show current round status");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        statusCmd.Add(projectOption);

        statusCmd.SetAction(async (parseResult) =>
        {
            var projectName = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var roundStore = scope.ServiceProvider.GetRequiredService<IRoundStore>();

            var config = await projectStore.LoadAsync(projectName).ConfigureAwait(false);
            if (config is null)
            {
                Console.WriteLine($"Project '{projectName}' not found.");
                return;
            }

            Console.WriteLine($"Project: {config.DisplayName}");
            Console.WriteLine($"  Status: {config.Status}");
            Console.WriteLine($"  Current round: {config.CurrentRound}");

            if (config.CurrentRound > 0)
            {
                var plan = await roundStore.GetPlanAsync(projectName, config.CurrentRound).ConfigureAwait(false);
                if (plan is not null)
                {
                    Console.WriteLine($"  Rationale: {plan.Rationale}");
                    Console.WriteLine($"  Dispatches: {plan.Dispatches.Count}");
                    Console.WriteLine($"  Human actions: {plan.HumanActions.Count}");
                }

                var findings = await roundStore.GetFindingsAsync(projectName, config.CurrentRound).ConfigureAwait(false);
                Console.WriteLine($"  Findings received: {findings.Count}");
            }
            else
            {
                Console.WriteLine("  No rounds executed yet.");
            }
        });

        return statusCmd;
    }
}
