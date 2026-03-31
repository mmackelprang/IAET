// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class SecretsCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("secrets", "Manage project secrets");
        cmd.Add(CreateSetCmd(services));
        cmd.Add(CreateGetCmd(services));
        cmd.Add(CreateListCmd(services));
        return cmd;
    }

    private static Command CreateSetCmd(IServiceProvider services)
    {
        var setCmd = new Command("set", "Set a secret value");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var keyOption = new Option<string>("--key") { Description = "Secret key", Required = true };
        var valueOption = new Option<string>("--value") { Description = "Secret value", Required = true };
        setCmd.Add(projectOption);
        setCmd.Add(keyOption);
        setCmd.Add(valueOption);

        setCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var key = parseResult.GetRequiredValue(keyOption);
            var value = parseResult.GetRequiredValue(valueOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISecretsStore>();
            await store.SetAsync(project, key, value).ConfigureAwait(false);
            Console.WriteLine($"Secret '{key}' set for project '{project}'.");
        });
        return setCmd;
    }

    private static Command CreateGetCmd(IServiceProvider services)
    {
        var getCmd = new Command("get", "Get a secret value");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var keyOption = new Option<string>("--key") { Description = "Secret key", Required = true };
        getCmd.Add(projectOption);
        getCmd.Add(keyOption);

        getCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var key = parseResult.GetRequiredValue(keyOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISecretsStore>();
            var value = await store.GetAsync(project, key).ConfigureAwait(false);

            if (value is null)
                Console.WriteLine($"Secret '{key}' not found in project '{project}'.");
            else
                Console.WriteLine(value);
        });
        return getCmd;
    }

    private static Command CreateListCmd(IServiceProvider services)
    {
        var listCmd = new Command("list", "List secret keys (values hidden)");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        listCmd.Add(projectOption);

        listCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISecretsStore>();
            var secrets = await store.ListAsync(project).ConfigureAwait(false);

            if (secrets.Count == 0)
            {
                Console.WriteLine($"No secrets found for project '{project}'.");
                return;
            }

            Console.WriteLine($"{"Key",-30} {"Value (preview)"}");
            Console.WriteLine(new string('-', 50));
            foreach (var (key, value) in secrets)
            {
                var preview = value.Length <= 4 ? "****" : value[..4] + new string('*', Math.Min(value.Length - 4, 20));
                Console.WriteLine($"{key,-30} {preview}");
            }
        });
        return listCmd;
    }
}
