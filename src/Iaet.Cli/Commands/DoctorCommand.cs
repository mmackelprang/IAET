// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

/// <summary>
/// <c>iaet doctor</c> — probes every external tool IAET can delegate to and reports which
/// capabilities are unlocked. Read-only: it never installs anything.
/// </summary>
internal static class DoctorCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("doctor", "Check external tools (jadx, apktool, Ghidra, Java, Node, Playwright) and report which IAET capabilities are available");
        var jsonOption = new Option<bool>("--json") { Description = "Emit machine-readable JSON instead of a table" };
        cmd.Add(jsonOption);

        cmd.SetAction(async (parseResult) =>
        {
            var asJson = parseResult.GetValue(jsonOption);
            var results = await RunChecksAsync(services).ConfigureAwait(false);

            if (asJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = results.All(r => r.Available || r.Optional),
                    checkedAt = DateTimeOffset.UtcNow,
                    tools = results,
                }, JsonOptions));
                return;
            }

            Console.WriteLine("IAET environment check");
            Console.WriteLine();
            foreach (var r in results)
            {
                var status = r.Available ? "OK     " : r.Optional ? "MISSING" : "ERROR  ";
                var version = string.IsNullOrEmpty(r.Version) ? string.Empty : $" {r.Version}";
                Console.WriteLine($"  [{status}] {r.Name}{version}");
                Console.WriteLine($"            unlocks: {string.Join(", ", r.Capabilities)}");
                if (!string.IsNullOrEmpty(r.ResolvedPath))
                    Console.WriteLine($"            path:    {r.ResolvedPath}");
                if (!r.Available && !string.IsNullOrEmpty(r.Message))
                    Console.WriteLine($"            fix:     {r.Message}");
            }
            Console.WriteLine();
            var missing = results.Where(r => !r.Available).Select(r => r.Name).ToList();
            Console.WriteLine(missing.Count == 0
                ? "All tools available."
                : $"Missing: {string.Join(", ", missing)}. Core web/APK workflows still run; the listed capabilities are unavailable.");
        });

        return cmd;
    }

    internal static async Task<IReadOnlyList<ToolHealth>> RunChecksAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var results = new List<ToolHealth>
        {
            new()
            {
                Name = "dotnet",
                Capabilities = ["all iaet commands"],
                Available = true,
                Version = Environment.Version.ToString(),
                Optional = false,
            },
            await ProbeAsync("java", ["apk decompile (jadx)", "native analyze --decompile (Ghidra)"], "java", "-version",
                "Install a JDK 21+ (https://adoptium.net). jadx needs 11+, Ghidra needs 21+.", ct).ConfigureAwait(false),
            await ProbeAsync("jadx", ["apk decompile", "apk analyze", "apk ble"], "jadx", "--version",
                "Install jadx from https://github.com/skylot/jadx/releases and put it on PATH, or pass --jadx-path.", ct).ConfigureAwait(false),
            await ProbeAsync("apktool", ["apk resources fallback (manifest, res/)"], "apktool", "--version",
                "Install apktool from https://apktool.org (optional fallback when jadx cannot decode resources).", ct).ConfigureAwait(false),
            await ProbeAsync("node", ["browser extension build", "crawl recipes (TypeScript)"], "node", "--version",
                "Install Node.js 20+ from https://nodejs.org.", ct).ConfigureAwait(false),
            ProbePlaywright(),
            await ProbeAsync("python3", ["scripts/generate-dashboard.py"], OperatingSystem.IsWindows() ? "python" : "python3", "--version",
                "Install Python 3.10+ (only needed for the legacy static dashboard script).", ct).ConfigureAwait(false),
        };

        using var scope = services.CreateScope();
        foreach (var provider in scope.ServiceProvider.GetServices<IToolProvider>())
            results.Add(await provider.CheckAsync(ct).ConfigureAwait(false));

        return results;
    }

    private static async Task<ToolHealth> ProbeAsync(string name, string[] capabilities, string executable, string versionArg, string installHint, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo(executable, versionArg)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return Missing(name, capabilities, installHint);

            var stdout = proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = proc.StandardError.ReadToEndAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await proc.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = (await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false)).Trim();
            // JVMs echo "Picked up JAVA_TOOL_OPTIONS: ..." before the real version line; skip such noise.
            var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .FirstOrDefault(l => !l.StartsWith("Picked up ", StringComparison.Ordinal));

            return new ToolHealth
            {
                Name = name,
                Capabilities = capabilities,
                Available = true,
                Version = firstLine,
                ResolvedPath = executable,
            };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException or IOException)
        {
            return Missing(name, capabilities, installHint);
        }
    }

    private static ToolHealth Missing(string name, string[] capabilities, string hint) => new()
    {
        Name = name,
        Capabilities = capabilities,
        Available = false,
        Message = hint,
    };

    private static ToolHealth ProbePlaywright()
    {
        string[] capabilities = ["capture start", "crawl"];
        var roots = new List<string>();
        var env = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (!string.IsNullOrEmpty(env))
            roots.Add(env);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        roots.Add(Path.Combine(home, ".cache", "ms-playwright"));
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright"));
        roots.Add(Path.Combine(home, "Library", "Caches", "ms-playwright"));

        foreach (var root in roots.Where(Directory.Exists))
        {
            var chromium = Directory.EnumerateDirectories(root, "chromium*").FirstOrDefault();
            if (chromium is not null)
            {
                return new ToolHealth
                {
                    Name = "playwright-chromium",
                    Capabilities = capabilities,
                    Available = true,
                    ResolvedPath = chromium,
                    Version = Path.GetFileName(chromium),
                };
            }
        }

        return new ToolHealth
        {
            Name = "playwright-chromium",
            Capabilities = capabilities,
            Available = false,
            Message = "Run `pwsh bin/Release/net10.0/playwright.ps1 install chromium` (or `npx playwright install chromium`). The browser extension capture path works without it.",
        };
    }
}
