// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class ProjectCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("project", "Manage investigation projects");
        cmd.Add(CreateCreateCmd(services));
        cmd.Add(CreateListCmd(services));
        cmd.Add(CreateStatusCmd(services));
        cmd.Add(CreateArchiveCmd(services));
        cmd.Add(CreateCompleteCmd(services));
        cmd.Add(CreateRerunCmd(services));
        return cmd;
    }

    private static Command CreateCreateCmd(IServiceProvider services)
    {
        var createCmd = new Command("create", "Create a new investigation project");
        var nameOption = new Option<string>("--name") { Description = "Project name (slug)", Required = true };
        var urlOption = new Option<string>("--url") { Description = "Target starting URL", Required = true };
        var targetTypeOption = new Option<string>("--target-type") { Description = "Target type: web, android, desktop", DefaultValueFactory = _ => "web" };
        var authRequiredOption = new Option<bool>("--auth-required") { Description = "Target requires authentication" };
        var displayNameOption = new Option<string?>("--display-name") { Description = "Human-readable project name" };

        createCmd.Add(nameOption);
        createCmd.Add(urlOption);
        createCmd.Add(targetTypeOption);
        createCmd.Add(authRequiredOption);
        createCmd.Add(displayNameOption);

        createCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);
            var url = parseResult.GetRequiredValue(urlOption);
            var targetTypeStr = parseResult.GetValue(targetTypeOption)!;
            var authRequired = parseResult.GetValue(authRequiredOption);
            var displayName = parseResult.GetValue(displayNameOption) ?? name;

            if (!Enum.TryParse<TargetType>(targetTypeStr, ignoreCase: true, out var targetType))
            {
                Console.WriteLine($"Unknown target type: {targetTypeStr}. Use: web, android, desktop.");
                return;
            }

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();

            var config = new ProjectConfig
            {
                Name = name,
                DisplayName = displayName,
                TargetType = targetType,
                EntryPoints = [new EntryPoint { Url = url, Label = "Main" }],
                AuthRequired = authRequired,
                AuthMethod = authRequired ? "browser-login" : null,
            };

            await store.CreateAsync(config).ConfigureAwait(false);
            GitGuard.EnsureGitignore(Directory.GetCurrentDirectory());

            Console.WriteLine($"Created project: {name}");
            Console.WriteLine($"  Target: {url} ({targetType}, {(authRequired ? "auth-required" : "no-auth")})");
            Console.WriteLine($"  Project dir: {store.GetProjectDirectory(name)}");
        });

        return createCmd;
    }

    private static Command CreateListCmd(IServiceProvider services)
    {
        var listCmd = new Command("list", "List all projects");
        listCmd.SetAction(async (_) =>
        {
            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var projects = await store.ListAsync().ConfigureAwait(false);

            if (projects.Count == 0)
            {
                Console.WriteLine("No projects found.");
                return;
            }

            Console.WriteLine($"{"Name",-25} {"Type",-10} {"Status",-15} {"Rounds",-8} {"Last Activity"}");
            Console.WriteLine(new string('-', 85));
            foreach (var p in projects)
            {
                Console.WriteLine($"{p.Name,-25} {p.TargetType,-10} {p.Status,-15} {p.CurrentRound,-8} {p.LastActivityAt:g}");
            }
        });
        return listCmd;
    }

    private static Command CreateStatusCmd(IServiceProvider services)
    {
        var statusCmd = new Command("status", "Show project status");
        var nameOption = new Option<string>("--name") { Description = "Project name", Required = true };
        statusCmd.Add(nameOption);

        statusCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);
            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var loaded = await store.LoadAsync(name).ConfigureAwait(false);

            if (loaded is null)
            {
                Console.WriteLine($"Project '{name}' not found.");
                return;
            }

            // Refresh status from actual project content before displaying
            var config = await store.RefreshStatusAsync(name).ConfigureAwait(false);

            Console.WriteLine($"Project: {config.DisplayName}");
            Console.WriteLine($"  Name:      {config.Name}");
            Console.WriteLine($"  Type:      {config.TargetType}");
            Console.WriteLine($"  Status:    {config.Status}");
            Console.WriteLine($"  Round:     {config.CurrentRound}");
            Console.WriteLine($"  Auth:      {(config.AuthRequired ? "required" : "none")}");
            Console.WriteLine($"  Created:   {config.CreatedAt:g}");
            Console.WriteLine($"  Last:      {config.LastActivityAt:g}");
            Console.WriteLine($"  Targets:");
            foreach (var ep in config.EntryPoints)
                Console.WriteLine($"    {ep.Label}: {ep.Url}");
        });
        return statusCmd;
    }

    private static Command CreateArchiveCmd(IServiceProvider services)
    {
        var archiveCmd = new Command("archive", "Archive a project");
        var nameOption = new Option<string>("--name") { Description = "Project name", Required = true };
        archiveCmd.Add(nameOption);

        archiveCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);
            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            try
            {
                await store.ArchiveAsync(name).ConfigureAwait(false);
                Console.WriteLine($"Project '{name}' archived.");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }
        });
        return archiveCmd;
    }

    private static Command CreateCompleteCmd(IServiceProvider services)
    {
        var completeCmd = new Command("complete", "Mark a project as complete");
        var nameOption = new Option<string>("--name") { Description = "Project name", Required = true };
        completeCmd.Add(nameOption);

        completeCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);
            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await store.LoadAsync(name).ConfigureAwait(false);
            if (config is null)
            {
                Console.WriteLine($"Project '{name}' not found.");
                return;
            }

            var updated = config with { Status = ProjectStatus.Complete, LastActivityAt = DateTimeOffset.UtcNow };
            await store.SaveAsync(updated).ConfigureAwait(false);
            Console.WriteLine($"Project '{name}' marked as complete.");
        });
        return completeCmd;
    }

    private static Command CreateRerunCmd(IServiceProvider services)
    {
        var rerunCmd = new Command("rerun", "Re-enable a project for further investigation");
        var nameOption = new Option<string>("--name") { Description = "Project name", Required = true };
        rerunCmd.Add(nameOption);

        rerunCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);
            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await store.LoadAsync(name).ConfigureAwait(false);
            if (config is null)
            {
                Console.WriteLine($"Project '{name}' not found.");
                return;
            }

            var updated = config with { Status = ProjectStatus.Investigating, LastActivityAt = DateTimeOffset.UtcNow };
            await store.SaveAsync(updated).ConfigureAwait(false);
            Console.WriteLine($"Project '{name}' status set to Investigating.");
        });
        return rerunCmd;
    }
}
