// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Diagnostics;
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class DashboardCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("dashboard", "Generate and open the investigation dashboard");

        var projectOption = new Option<string?>("--project") { Description = "Project name (default: all projects)" };
        var openOption = new Option<bool>("--open") { Description = "Open dashboard in browser", DefaultValueFactory = _ => true };

        cmd.Add(projectOption);
        cmd.Add(openOption);

        cmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetValue(projectOption);
            var open = parseResult.GetValue(openOption);

            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();

            string projectDir;
            if (project is not null)
            {
                var config = await projectStore.LoadAsync(project).ConfigureAwait(false);
                if (config is null)
                {
                    Console.WriteLine($"Project '{project}' not found.");
                    return;
                }
                projectDir = projectStore.GetProjectDirectory(project);
            }
            else
            {
                // Root of all projects: parent of any single project directory
                projectDir = Path.GetDirectoryName(projectStore.GetProjectDirectory("_"))!;
            }

            var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "generate-dashboard.py");

            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"Dashboard script not found at: {scriptPath}");
                return;
            }

            var args = project is not null
                ? $"\"{scriptPath}\" \"{projectDir}\""
                : $"\"{scriptPath}\"";

            Console.WriteLine("Generating dashboard...");

            var psi = new ProcessStartInfo("python3", args)
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                Console.WriteLine("Failed to start Python. Ensure python3 is on PATH.");
                return;
            }

            var stdout = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var stderr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await proc.WaitForExitAsync().ConfigureAwait(false);

            if (proc.ExitCode != 0)
            {
                Console.WriteLine($"Dashboard generation failed: {stderr}");
                return;
            }

            Console.WriteLine(stdout.Trim());

            if (open)
            {
                var outputDir = project is not null
                    ? Path.Combine(projectDir, "output")
                    : Path.Combine(Directory.GetCurrentDirectory(), ".iaet-projects");
                var dashboardPath = Path.Combine(outputDir, "dashboard.html");

                if (File.Exists(dashboardPath))
                {
                    Process.Start(new ProcessStartInfo(dashboardPath) { UseShellExecute = true });
                    Console.WriteLine("Dashboard opened in browser.");
                }
            }
        });

        return cmd;
    }
}
