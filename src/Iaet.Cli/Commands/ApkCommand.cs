// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Android.Bluetooth;
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
        cmd.Add(CreateBleCmd(services));
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
                await projectStore.RefreshStatusAsync(project).ConfigureAwait(false);
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
        var traceDataflowOption = new Option<bool>("--trace-dataflow") { Description = "Trace network data flow through response handlers to UI bindings" };

        analyzeCmd.Add(projectOption);
        analyzeCmd.Add(traceDataflowOption);

        analyzeCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var traceDataflow = parseResult.GetValue(traceDataflowOption);

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

            // Network data flow tracing (optional)
            var networkFlows = new List<NetworkDataFlow>();
            if (traceDataflow)
            {
                Console.WriteLine("  Tracing network data flows...");
                networkFlows.AddRange(NetworkDataFlowTracer.TraceFromDirectory(decompiledDir));
                Console.WriteLine($"  Found {networkFlows.Count} network data flows");
            }

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

            // network-data-flows.json (when --trace-dataflow is enabled)
            if (traceDataflow && networkFlows.Count > 0)
            {
#pragma warning disable CA1308 // Confidence displayed lowercase in JSON for readability
                var flowsObj = new
                {
                    flows = networkFlows.Select(f => new
                    {
                        sourceType = f.SourceType,
                        sourceFile = f.SourceFile,
                        responseHandler = f.ResponseHandler,
                        parsing = f.ParsingDescription,
                        targetVariable = f.TargetVariable,
                        uiBinding = f.UiBinding,
                        inferredPurpose = f.InferredPurpose,
                        confidence = f.Confidence.ToString().ToLowerInvariant(),
                    }).ToList(),
                };
#pragma warning restore CA1308
                await File.WriteAllTextAsync(
                    Path.Combine(knowledgeDir, "network-data-flows.json"),
                    JsonSerializer.Serialize(flowsObj, JsonOptions)).ConfigureAwait(false);
            }

            Console.WriteLine();
            Console.WriteLine("Analysis complete. Knowledge base updated.");
            Console.WriteLine($"  Endpoints:   {urls.Count}");
            Console.WriteLine($"  Auth:        {authEntries.Count}");
            Console.WriteLine($"  Permissions: {manifest.Permissions.Count}");
            Console.WriteLine($"  Pinned:      {netSecurity.PinnedDomains.Count} domains");
            if (traceDataflow)
                Console.WriteLine($"  Net flows:   {networkFlows.Count}");

            await projectStore.RefreshStatusAsync(project).ConfigureAwait(false);
        });

        return analyzeCmd;
    }

    private static Command CreateBleCmd(IServiceProvider services)
    {
        var bleCmd = new Command("ble", "Discover BLE services and characteristics from decompiled source");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var traceDataflowOption = new Option<bool>("--trace-dataflow") { Description = "Trace BLE data flow through callbacks and UI bindings" };
        var hciLogOption = new Option<FileInfo?>("--hci-log") { Description = "Path to btsnoop_hci.log for runtime correlation" };

        bleCmd.Add(projectOption);
        bleCmd.Add(traceDataflowOption);
        bleCmd.Add(hciLogOption);

        bleCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var traceDataflow = parseResult.GetValue(traceDataflowOption);
            var hciLogFile = parseResult.GetValue(hciLogOption);

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

            if (!Directory.Exists(decompiledDir))
            {
                Console.WriteLine("No decompiled source found. Run 'iaet apk decompile' first.");
                return;
            }

            Console.WriteLine("Scanning for BLE services...");
            var result = BleServiceExtractor.ExtractFromDirectory(decompiledDir);

            Console.WriteLine($"  Services:        {result.Services.Count}");
            Console.WriteLine($"  Characteristics: {result.Characteristics.Count}");

            IReadOnlyList<Iaet.Core.Models.BleDataFlow> bleFlows = [];
            if (traceDataflow)
            {
                Console.WriteLine("  Tracing BLE data flows...");
                bleFlows = BleDataFlowTracer.TraceFromDirectory(decompiledDir);
                Console.WriteLine($"  Found {bleFlows.Count} BLE data flows");
            }

            // HCI log import and correlation
            HciLogResult? hciResult = null;
            BleCorrelationResult? correlation = null;
            if (hciLogFile is not null)
            {
                Console.WriteLine($"  Importing HCI log: {hciLogFile.FullName}");
                hciResult = HciLogImporter.ParseFile(hciLogFile.FullName);

                if (hciResult.Errors.Count > 0)
                {
                    foreach (var error in hciResult.Errors)
                        Console.WriteLine($"  HCI log error: {error}");
                }
                else
                {
                    Console.WriteLine($"  HCI packets: {hciResult.TotalPackets} total, {hciResult.AttPackets} ATT");
                    correlation = BleCorrelator.Correlate(result.Services, hciResult);
                    Console.WriteLine($"  Correlation: {correlation.StaticOnlyCount} static, {correlation.RuntimeOnlyCount} runtime handles");
                }
            }

            // Write knowledge/bluetooth.json
            var knowledgeDir = Path.Combine(projectDir, "knowledge");
            Directory.CreateDirectory(knowledgeDir);

#pragma warning disable CA1308 // Confidence/operation names displayed lowercase in JSON for readability
            var bleObj = new
            {
                services = result.Services.Select(s => new
                {
                    uuid = s.Uuid,
                    name = s.Name,
                    isStandard = s.IsStandardService,
                    confidence = s.Confidence.ToString().ToLowerInvariant(),
                    source = s.SourceFile,
                    characteristics = s.Characteristics.Select(c => new
                    {
                        uuid = c.Uuid,
                        name = c.Name,
                        operations = c.Operations.Select(o => o.ToString().ToLowerInvariant()).ToList(),
                        source = c.SourceFile,
                    }).ToList(),
                }).ToList(),
                characteristics = result.Characteristics.Select(c => new
                {
                    uuid = c.Uuid,
                    name = c.Name,
                    operations = c.Operations.Select(o => o.ToString().ToLowerInvariant()).ToList(),
                    source = c.SourceFile,
                }).ToList(),
                hciLog = hciResult is not null && hciResult.Errors.Count == 0
                    ? new
                    {
                        totalPackets = hciResult.TotalPackets,
                        attPackets = hciResult.AttPackets,
                        operations = hciResult.Operations.Select(o => new
                        {
                            timestamp = o.Timestamp,
                            type = o.Type.ToLowerInvariant(),
                            handle = $"0x{o.Handle:X4}",
                            direction = o.IsReceived ? "received" : "sent",
                            valueLength = o.ValueLength,
                        }).ToList(),
                    }
                    : null,
                correlation = correlation is not null
                    ? new
                    {
                        staticOnlyCount = correlation.StaticOnlyCount,
                        runtimeOnlyCount = correlation.RuntimeOnlyCount,
                        correlatedCount = correlation.CorrelatedCount,
                    }
                    : null,
            };
#pragma warning restore CA1308

            var outputPath = Path.Combine(knowledgeDir, "bluetooth.json");
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(bleObj, JsonOptions)).ConfigureAwait(false);

            if (traceDataflow && bleFlows.Count > 0)
            {
#pragma warning disable CA1308 // Confidence displayed lowercase in JSON for readability
                var flowsObj = new
                {
                    flows = bleFlows.Select(f => new
                    {
                        characteristicUuid = f.CharacteristicUuid,
                        callbackLocation = f.CallbackLocation,
                        parsing = f.ParsingDescription,
                        variable = f.VariableName,
                        uiBinding = f.UiBinding,
                        inferredMeaning = f.InferredMeaning,
                        confidence = f.Confidence.ToString().ToLowerInvariant(),
                    }).ToList(),
                };
#pragma warning restore CA1308
                var flowsPath = Path.Combine(knowledgeDir, "ble-data-flows.json");
                await File.WriteAllTextAsync(
                    flowsPath,
                    JsonSerializer.Serialize(flowsObj, JsonOptions)).ConfigureAwait(false);
                Console.WriteLine($"BLE data flows written to: {flowsPath}");
            }

            Console.WriteLine();
            if (hciResult is not null && hciResult.Errors.Count == 0)
            {
                Console.WriteLine($"  HCI total:   {hciResult.TotalPackets} packets");
                Console.WriteLine($"  HCI ATT:     {hciResult.AttPackets} operations");
            }
            Console.WriteLine($"BLE analysis written to: {outputPath}");
        });

        return bleCmd;
    }
}
