// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Android.Decompilation;
using Iaet.Android.Extractors;
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class ApkCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("apk", "Android APK analysis");
        cmd.Add(CreateDecompileCmd(services));
        cmd.Add(CreateAnalyzeCmd(services));
        return cmd;
    }

    private static Command CreateDecompileCmd(IServiceProvider services)
    {
        var decompileCmd = new Command("decompile", "Decompile an APK file");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var apkOption = new Option<FileInfo>("--apk") { Description = "Path to APK file", Required = true };
        var jadxPathOption = new Option<string>("--jadx-path") { Description = "Path to jadx executable", DefaultValueFactory = _ => "jadx" };
        var mappingOption = new Option<FileInfo?>("--mapping") { Description = "ProGuard mapping.txt file" };

        decompileCmd.Add(projectOption);
        decompileCmd.Add(apkOption);
        decompileCmd.Add(jadxPathOption);
        decompileCmd.Add(mappingOption);

        decompileCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var apkFile = parseResult.GetRequiredValue(apkOption);
            var jadxPath = parseResult.GetValue(jadxPathOption)!;
            var mapping = parseResult.GetValue(mappingOption);

            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await projectStore.LoadAsync(project).ConfigureAwait(false);

            if (config is null)
            {
                Console.WriteLine($"Project '{project}' not found.");
                return;
            }

            var projectDir = projectStore.GetProjectDirectory(project);
            var apkDir = Path.Combine(projectDir, "apk");
            Directory.CreateDirectory(apkDir);

            // Copy APK to project
            var destApk = Path.Combine(apkDir, "app.apk");
            File.Copy(apkFile.FullName, destApk, overwrite: true);
            Console.WriteLine($"APK copied to: {destApk}");

            // Copy mapping if provided
            if (mapping is not null)
            {
                File.Copy(mapping.FullName, Path.Combine(apkDir, "mapping.txt"), overwrite: true);
                Console.WriteLine("ProGuard mapping.txt copied.");
            }

            // Run jadx
            Console.WriteLine("Decompiling with jadx...");
            var outputDir = Path.Combine(apkDir, "decompiled");
            var runner = new JadxRunner(jadxPath);
            var result = await runner.RunAsync(destApk, outputDir).ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine($"Decompilation complete: {result.FileCount} Java files in {result.DurationMs}ms");
                Console.WriteLine($"Output: {result.OutputDirectory}");
            }
            else
            {
                Console.WriteLine($"Decompilation failed: {result.ErrorMessage}");
            }
        });

        return decompileCmd;
    }

    private static Command CreateAnalyzeCmd(IServiceProvider services)
    {
        var analyzeCmd = new Command("analyze", "Analyze decompiled APK source");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };

        analyzeCmd.Add(projectOption);

        analyzeCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await projectStore.LoadAsync(project).ConfigureAwait(false);

            if (config is null)
            {
                Console.WriteLine($"Project '{project}' not found.");
                return;
            }

            var projectDir = projectStore.GetProjectDirectory(project);
            var decompiledDir = Path.Combine(projectDir, "apk", "decompiled");
            var resourcesDir = Path.Combine(projectDir, "apk", "resources");

            if (!Directory.Exists(decompiledDir))
            {
                Console.WriteLine("No decompiled source found. Run 'iaet apk decompile' first.");
                return;
            }

            Console.WriteLine("Analyzing decompiled source...");

            // URL extraction
            Console.WriteLine("  Extracting API endpoints...");
            var urls = ApkUrlExtractor.ExtractFromDirectory(decompiledDir);
            Console.WriteLine($"  Found {urls.Count} API URLs");

            // Auth extraction
            Console.WriteLine("  Extracting auth patterns...");
            var authEntries = new List<AuthEntry>();
            foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
            {
#pragma warning disable CA1849 // File scanning loop — ReadAllTextAsync not practical here without extra complexity
                var source = File.ReadAllText(file);
#pragma warning restore CA1849
                var relativePath = Path.GetRelativePath(decompiledDir, file);
                authEntries.AddRange(ApkAuthExtractor.Extract(source, relativePath));
            }
            Console.WriteLine($"  Found {authEntries.Count} auth entries");

            // Manifest analysis
            var manifestPath = Path.Combine(resourcesDir, "AndroidManifest.xml");
            var manifest = ManifestAnalyzer.ParseFile(manifestPath);
            if (manifest.PackageName != "unknown")
            {
                Console.WriteLine($"  Package: {manifest.PackageName} v{manifest.VersionName}");
                Console.WriteLine($"  Permissions: {manifest.Permissions.Count}");
            }

            // Network security
            var nscPath = Path.Combine(resourcesDir, "res", "xml", "network_security_config.xml");
            var netSecurity = NetworkSecurityAnalyzer.ParseFile(nscPath);

            // Write knowledge
            var knowledgeDir = Path.Combine(projectDir, "knowledge");
            Directory.CreateDirectory(knowledgeDir);

            // endpoints.json
#pragma warning disable CA1308 // Confidence is displayed in lowercase for JSON readability, not used for normalization
            var endpointsObj = new
            {
                endpoints = urls.Select(u => new
                {
                    signature = u.HttpMethod is not null ? $"{u.HttpMethod} {u.Url}" : u.Url,
                    confidence = u.Confidence.ToString().ToLowerInvariant(),
                    source = u.SourceFile,
                    context = u.Context,
                }).ToList(),
            };
#pragma warning restore CA1308
            await File.WriteAllTextAsync(
                Path.Combine(knowledgeDir, "endpoints.json"),
                JsonSerializer.Serialize(endpointsObj, JsonOptions)).ConfigureAwait(false);

            // permissions.json
            var permObj = new
            {
                packageName = manifest.PackageName,
                versionName = manifest.VersionName,
                minSdk = manifest.MinSdk,
                targetSdk = manifest.TargetSdk,
                permissions = manifest.Permissions,
                exportedServices = manifest.ExportedServices,
                exportedReceivers = manifest.ExportedReceivers,
            };
            await File.WriteAllTextAsync(
                Path.Combine(knowledgeDir, "permissions.json"),
                JsonSerializer.Serialize(permObj, JsonOptions)).ConfigureAwait(false);

            // network-security.json
            var nsObj = new
            {
                cleartextDefault = netSecurity.CleartextDefaultPermitted,
                cleartextDomains = netSecurity.CleartextPermittedDomains,
                pinnedDomains = netSecurity.PinnedDomains.Select(d => new { d.Domain, d.Pins }),
            };
            await File.WriteAllTextAsync(
                Path.Combine(knowledgeDir, "network-security.json"),
                JsonSerializer.Serialize(nsObj, JsonOptions)).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("Analysis complete. Knowledge base updated.");
            Console.WriteLine($"  Endpoints:   {urls.Count}");
            Console.WriteLine($"  Auth:        {authEntries.Count}");
            Console.WriteLine($"  Permissions: {manifest.Permissions.Count}");
            Console.WriteLine($"  Pinned:      {netSecurity.PinnedDomains.Count} domains");
        });

        return analyzeCmd;
    }
}
