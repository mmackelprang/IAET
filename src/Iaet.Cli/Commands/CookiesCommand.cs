// src/Iaet.Cli/Commands/CookiesCommand.cs
// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Cookies;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class CookiesCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("cookies", "Manage cookie capture and analysis");
        cmd.Add(CreateSnapshotCmd(services));
        cmd.Add(CreateDiffCmd(services));
        cmd.Add(CreateAnalyzeCmd(services));
        return cmd;
    }

    private static Command CreateSnapshotCmd(IServiceProvider services)
    {
        var snapshotCmd = new Command("snapshot", "List cookie snapshots for a project");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        snapshotCmd.Add(projectOption);

        snapshotCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ICookieStore>();
            var snapshots = await store.ListSnapshotsAsync(project).ConfigureAwait(false);

            if (snapshots.Count == 0)
            {
                Console.WriteLine($"No cookie snapshots found for project '{project}'.");
                return;
            }

            Console.WriteLine($"{"ID",-38} {"Source",-20} {"Cookies",-10} {"Captured At"}");
            Console.WriteLine(new string('-', 90));
            foreach (var s in snapshots)
            {
                Console.WriteLine($"{s.Id,-38} {s.Source,-20} {s.Cookies.Count,-10} {s.CapturedAt:g}");
            }
        });

        return snapshotCmd;
    }

    private static Command CreateDiffCmd(IServiceProvider services)
    {
        var diffCmd = new Command("diff", "Diff two cookie snapshots");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var beforeOption = new Option<Guid>("--before") { Description = "Before snapshot ID", Required = true };
        var afterOption = new Option<Guid>("--after") { Description = "After snapshot ID", Required = true };
        diffCmd.Add(projectOption);
        diffCmd.Add(beforeOption);
        diffCmd.Add(afterOption);

        diffCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var beforeId = parseResult.GetRequiredValue(beforeOption);
            var afterId = parseResult.GetRequiredValue(afterOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ICookieStore>();

            var before = await store.GetSnapshotAsync(project, beforeId).ConfigureAwait(false);
            var after = await store.GetSnapshotAsync(project, afterId).ConfigureAwait(false);

            if (before is null || after is null)
            {
                Console.WriteLine("One or both snapshots not found.");
                return;
            }

            var diff = CookieDiffer.Diff(before, after);

            Console.WriteLine($"Cookie diff: {before.Source} → {after.Source}");
            Console.WriteLine($"  Added:   {diff.Added.Count}");
            Console.WriteLine($"  Removed: {diff.Removed.Count}");
            Console.WriteLine($"  Changed: {diff.Changed.Count}");

            if (diff.Added.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Added cookies:");
                foreach (var c in diff.Added)
                    Console.WriteLine($"    + {c.Name} ({c.Domain})");
            }

            if (diff.Removed.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Removed cookies:");
                foreach (var c in diff.Removed)
                    Console.WriteLine($"    - {c.Name} ({c.Domain})");
            }

            if (diff.Changed.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Changed cookies:");
                foreach (var c in diff.Changed)
                    Console.WriteLine($"    ~ {c.Name} ({c.Domain})");
            }
        });

        return diffCmd;
    }

    private static Command CreateAnalyzeCmd(IServiceProvider services)
    {
        var analyzeCmd = new Command("analyze", "Analyze cookie lifecycle for a project");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        analyzeCmd.Add(projectOption);

        analyzeCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ICookieStore>();
            var snapshots = await store.ListSnapshotsAsync(project).ConfigureAwait(false);

            if (snapshots.Count == 0)
            {
                Console.WriteLine($"No snapshots found for project '{project}'.");
                return;
            }

            var latest = snapshots.OrderByDescending(s => s.CapturedAt).First();
            var analysis = CookieLifecycleAnalyzer.Analyze(project, latest);

            Console.WriteLine($"Cookie analysis for: {project}");
            Console.WriteLine($"  Total cookies: {analysis.TotalCookies}");

            if (analysis.ExpiringWithin.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Expiring soon:");
                foreach (var (name, remaining) in analysis.ExpiringWithin)
                    Console.WriteLine($"    ⚠ {name} — {remaining.TotalMinutes:F0} minutes remaining");
            }

            if (snapshots.Count >= 2)
            {
                var ordered = snapshots.OrderBy(s => s.CapturedAt).ToList();
                var rotation = CookieLifecycleAnalyzer.AnalyzeRotation(project, ordered);

                if (rotation.RotationDetected.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("  Rotation detected:");
                    foreach (var name in rotation.RotationDetected)
                        Console.WriteLine($"    ↻ {name}");
                }
            }
        });

        return analyzeCmd;
    }
}
