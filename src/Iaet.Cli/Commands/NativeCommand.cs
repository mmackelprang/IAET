// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.Native;
using Iaet.Native.Ghidra;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

/// <summary>
/// <c>iaet native</c> — native binary analysis (APK <c>lib/*.so</c>, desktop executables, firmware blobs).
/// Static providers always run; Ghidra adds functions, call graphs and pseudocode when installed.
/// </summary>
internal static class NativeCommand
{
    private static readonly JsonSerializerOptions JsonOptions = NativeBinaryAnalyzer.JsonOptions;

    internal const string KnowledgeFile = "native-binaries.json";
    internal const string UnknownsFile = "unknowns.json";

    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("native", "Native binary analysis (ELF/PE/Mach-O, APK native libraries, Ghidra headless)");
        cmd.Add(CreateAnalyzeCmd(services));
        cmd.Add(CreateSearchCmd(services));
        cmd.Add(CreateFunctionCmd(services));
        return cmd;
    }

    private static Command CreateAnalyzeCmd(IServiceProvider services)
    {
        var analyzeCmd = new Command("analyze", "Analyze native binaries: auto-discovers APK lib/*.so or takes explicit --binary paths");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var binaryOption = new Option<FileInfo[]>("--binary") { Description = "Binary file(s) to analyze (repeatable). Default: native libraries found under the project's apk/ directory", AllowMultipleArgumentsPerToken = true };
        var ghidraPathOption = new Option<string?>("--ghidra-path") { Description = "Ghidra install dir or analyzeHeadless path (default: IAET_GHIDRA_PATH / GHIDRA_INSTALL_DIR / PATH)" };
        var noGhidraOption = new Option<bool>("--no-ghidra") { Description = "Skip the Ghidra provider even if installed (static ELF/strings analysis only)" };
        var decompileOption = new Option<bool>("--decompile") { Description = "Export decompiled pseudocode per function (slower; requires Ghidra)" };
        var maxFunctionsOption = new Option<int>("--max-functions") { Description = "Maximum functions to export from Ghidra", DefaultValueFactory = _ => 2000 };
        var noCacheOption = new Option<bool>("--no-cache") { Description = "Ignore cached Ghidra exports for identical bytes/options" };

        analyzeCmd.Add(projectOption);
        analyzeCmd.Add(binaryOption);
        analyzeCmd.Add(ghidraPathOption);
        analyzeCmd.Add(noGhidraOption);
        analyzeCmd.Add(decompileOption);
        analyzeCmd.Add(maxFunctionsOption);
        analyzeCmd.Add(noCacheOption);

        analyzeCmd.SetAction(async (parseResult, ct) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var binaries = parseResult.GetValue(binaryOption) ?? [];
            var options = new NativeAnalysisOptions
            {
                GhidraPath = parseResult.GetValue(ghidraPathOption),
                UseGhidra = !parseResult.GetValue(noGhidraOption),
                Decompile = parseResult.GetValue(decompileOption),
                MaxFunctions = parseResult.GetValue(maxFunctionsOption),
                UseCache = !parseResult.GetValue(noCacheOption),
            };

            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await projectStore.LoadAsync(project, ct).ConfigureAwait(false);
            if (config is null)
            {
                Console.WriteLine($"Project '{project}' not found.");
                return;
            }

            var projectDir = projectStore.GetProjectDirectory(project);
            var nativeDir = Path.Combine(projectDir, "native");
            Directory.CreateDirectory(nativeDir);
            options = options with { WorkDirectory = Path.Combine(nativeDir, "work") };

            var targets = new List<(string Path, string? Abi)>();
            if (binaries.Length > 0)
            {
                foreach (var b in binaries)
                {
                    if (!b.Exists)
                    {
                        Console.WriteLine($"Binary not found: {b.FullName}");
                        continue;
                    }
                    // Copy into the project so the analysis stays reproducible after the source moves.
                    var dest = Path.Combine(nativeDir, "binaries", b.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(b.FullName, dest, overwrite: true);
                    targets.Add((dest, null));
                }
            }
            else
            {
                var libs = NativeBinaryAnalyzer.DiscoverApkLibraries(Path.Combine(projectDir, "apk"));
                if (libs.Count == 0)
                {
                    Console.WriteLine("No native libraries found under apk/. Pass --binary <path> or run 'iaet apk decompile' first.");
                    return;
                }
                Console.WriteLine($"Discovered {libs.Count} native librar{(libs.Count == 1 ? "y" : "ies")} in the APK (best ABI per library).");
                targets.AddRange(libs.Select(l => (l.Path, (string?)l.Abi)));
            }

            if (options.UseGhidra)
            {
                var launcher = GhidraLocator.FindAnalyzeHeadless(options.GhidraPath);
                Console.WriteLine(launcher is null
                    ? "Ghidra: not found — running static providers only (set GHIDRA_INSTALL_DIR or pass --ghidra-path for functions and pseudocode)."
                    : $"Ghidra: {launcher}");
            }

            var results = new List<NativeBinaryInfo>();
            foreach (var (path, abi) in targets)
            {
                var rel = Path.GetRelativePath(projectDir, path).Replace('\\', '/');
                Console.WriteLine($"Analyzing {rel}{(abi is null ? string.Empty : $" [{abi}]")}...");
                var info = await NativeBinaryAnalyzer.AnalyzeAsync(path, rel, abi, options, ct).ConfigureAwait(false);
                results.Add(info);
                Console.WriteLine($"  {info.Format} {info.Architecture} {info.Bitness}-bit | imports {info.Imports.Count}, exports {info.Exports.Count}, JNI {info.JniBindings.Count}, URLs {info.Urls.Count}, BLE UUIDs {info.BleUuids.Count(Iaet.Native.Extractors.NativeArtifactExtractor.IsBluetoothBaseUuid)}, libs {string.Join('/', info.Libraries.Select(l => l.Library))}");
                if (info.Functions.Count > 0)
                    Console.WriteLine($"  Ghidra: {info.Functions.Count} functions{(info.Functions.Any(f => f.Pseudocode is not null) ? " with pseudocode" : string.Empty)}");
                foreach (var lim in info.Limitations)
                    Console.WriteLine($"  limitation: {lim}");
            }

            var knowledgeDir = Path.Combine(projectDir, "knowledge");
            Directory.CreateDirectory(knowledgeDir);
            await File.WriteAllTextAsync(
                Path.Combine(knowledgeDir, KnowledgeFile),
                JsonSerializer.Serialize(new { analyzedAt = DateTimeOffset.UtcNow, binaries = results }, JsonOptions),
                ct).ConfigureAwait(false);

            var unknownsAdded = await MergeUnknownsAsync(Path.Combine(knowledgeDir, UnknownsFile), results.SelectMany(r => r.Unknowns), ct).ConfigureAwait(false);

            // Diagram: Java ↔ native bridge, when JNI bindings exist.
            var jniBindings = results.SelectMany(r => r.JniBindings.Select(j => (Library: Path.GetFileName(r.Path), Binding: j))).ToList();
            if (jniBindings.Count > 0)
            {
                var diagramsDir = Path.Combine(projectDir, "output", "diagrams");
                Directory.CreateDirectory(diagramsDir);
                await File.WriteAllTextAsync(Path.Combine(diagramsDir, "jni-bridge.mmd"), BuildJniDiagram(project, jniBindings), ct).ConfigureAwait(false);
                Console.WriteLine($"  Diagram:     {Path.Combine(diagramsDir, "jni-bridge.mmd")}");
            }

            Console.WriteLine();
            Console.WriteLine("Native analysis complete. Knowledge base updated.");
            Console.WriteLine($"  Binaries:    {results.Count}");
            Console.WriteLine($"  JNI exports: {results.Sum(r => r.JniBindings.Count)}");
            Console.WriteLine($"  URLs:        {results.Sum(r => r.Urls.Count)}");
            Console.WriteLine($"  Functions:   {results.Sum(r => r.Functions.Count)}");
            Console.WriteLine($"  Unknowns:    {unknownsAdded} new (knowledge/{UnknownsFile})");

            await projectStore.RefreshStatusAsync(project, ct).ConfigureAwait(false);
        });

        return analyzeCmd;
    }

    private static Command CreateSearchCmd(IServiceProvider services)
    {
        var searchCmd = new Command("search", "Bounded search across native strings, symbols, JNI bindings and function names in knowledge/native-binaries.json");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var queryOption = new Option<string>("--query") { Description = "Case-insensitive substring to look for", Required = true };
        var limitOption = new Option<int>("--limit") { Description = "Maximum hits to print", DefaultValueFactory = _ => 50 };
        searchCmd.Add(projectOption);
        searchCmd.Add(queryOption);
        searchCmd.Add(limitOption);

        searchCmd.SetAction(async (parseResult, ct) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var query = parseResult.GetRequiredValue(queryOption);
            var limit = parseResult.GetValue(limitOption);

            var binaries = await LoadKnowledgeAsync(services, project, ct).ConfigureAwait(false);
            if (binaries is null)
                return;

            var hits = Search(binaries, query).Take(limit).ToList();
            if (hits.Count == 0)
            {
                Console.WriteLine($"No matches for '{query}'.");
                return;
            }
            foreach (var hit in hits)
                Console.WriteLine($"{hit.Binary}  {hit.Kind,-9} {hit.Value}");
            Console.WriteLine();
            Console.WriteLine($"{hits.Count} hit(s) shown (limit {limit}). Strings inside binaries are not stored in knowledge; re-run `iaet native analyze` with Ghidra for function-referenced strings.");
        });

        return searchCmd;
    }

    private static Command CreateFunctionCmd(IServiceProvider services)
    {
        var fnCmd = new Command("function", "Print a function dossier (callers, callees, referenced strings, pseudocode) from the Ghidra export");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var nameOption = new Option<string>("--name") { Description = "Function name or 0x entry address", Required = true };
        var binaryOption = new Option<string?>("--binary") { Description = "Restrict to one binary file name (e.g. libnative.so)" };
        fnCmd.Add(projectOption);
        fnCmd.Add(nameOption);
        fnCmd.Add(binaryOption);

        fnCmd.SetAction(async (parseResult, ct) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var name = parseResult.GetRequiredValue(nameOption);
            var binaryFilter = parseResult.GetValue(binaryOption);

            var binaries = await LoadKnowledgeAsync(services, project, ct).ConfigureAwait(false);
            if (binaries is null)
                return;

            var matches = binaries
                .Where(b => binaryFilter is null || Path.GetFileName(b.Path).Equals(binaryFilter, StringComparison.OrdinalIgnoreCase))
                .SelectMany(b => b.Functions.Where(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || f.EntryAddress.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(f => (Binary: b, Function: f)))
                .ToList();

            if (matches.Count == 0)
            {
                var anyFunctions = binaries.Any(b => b.Functions.Count > 0);
                Console.WriteLine(anyFunctions
                    ? $"No function named '{name}'. Try `iaet native search --project {project} --query {name}`."
                    : "No functions in knowledge. Run `iaet native analyze` with Ghidra available (optionally --decompile).");
                return;
            }

            foreach (var (binary, fn) in matches)
            {
                Console.WriteLine($"## {fn.Name} @ {fn.EntryAddress}  ({Path.GetFileName(binary.Path)}, {fn.Size} bytes{(fn.IsThunk ? ", thunk" : string.Empty)}{(fn.IsExternal ? ", external" : string.Empty)})");
                Console.WriteLine($"Callers ({fn.Callers.Count}): {(fn.Callers.Count == 0 ? "—" : string.Join(", ", fn.Callers))}");
                Console.WriteLine($"Callees ({fn.Callees.Count}): {(fn.Callees.Count == 0 ? "—" : string.Join(", ", fn.Callees))}");
                if (fn.ReferencedStrings.Count > 0)
                {
                    Console.WriteLine("Strings:");
                    foreach (var s in fn.ReferencedStrings)
                        Console.WriteLine($"  \"{s}\"");
                }
                var jni = binary.JniBindings.FirstOrDefault(j => j.Symbol.Equals(fn.Name, StringComparison.Ordinal));
                if (jni is not null)
                    Console.WriteLine($"JNI: {jni.JavaClass}.{jni.JavaMethod}{(jni.Signature is null ? string.Empty : $" ({jni.Signature})")}");
                Console.WriteLine();
                Console.WriteLine(fn.Pseudocode ?? "(no pseudocode — re-run `iaet native analyze --decompile`)");
                Console.WriteLine();
                Console.WriteLine("Evidence: static decompilation by Ghidra; resolved calls only, no runtime confirmation.");
            }
        });

        return fnCmd;
    }

    internal static IEnumerable<(string Binary, string Kind, string Value)> Search(IReadOnlyList<NativeBinaryInfo> binaries, string query)
    {
        foreach (var b in binaries)
        {
            var name = Path.GetFileName(b.Path);
            foreach (var s in b.Exports.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                yield return (name, "export", s.Name);
            foreach (var s in b.Imports.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                yield return (name, "import", s.Name);
            foreach (var j in b.JniBindings.Where(j => j.JavaClass.Contains(query, StringComparison.OrdinalIgnoreCase) || j.JavaMethod.Contains(query, StringComparison.OrdinalIgnoreCase)))
                yield return (name, "jni", $"{j.JavaClass}.{j.JavaMethod} -> {j.Symbol}");
            foreach (var u in b.Urls.Where(u => u.Url.Contains(query, StringComparison.OrdinalIgnoreCase)))
                yield return (name, "url", u.Url);
            foreach (var u in b.BleUuids.Where(u => u.Contains(query, StringComparison.OrdinalIgnoreCase)))
                yield return (name, "ble-uuid", u);
            foreach (var f in b.Functions)
            {
                if (f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    yield return (name, "function", $"{f.Name} @ {f.EntryAddress}");
                foreach (var s in f.ReferencedStrings.Where(s => s.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    yield return (name, "string", $"\"{s}\" in {f.Name}");
            }
        }
    }

    private static async Task<IReadOnlyList<NativeBinaryInfo>?> LoadKnowledgeAsync(IServiceProvider services, string project, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
        var config = await projectStore.LoadAsync(project, ct).ConfigureAwait(false);
        if (config is null)
        {
            Console.WriteLine($"Project '{project}' not found.");
            return null;
        }

        var path = Path.Combine(projectStore.GetProjectDirectory(project), "knowledge", KnowledgeFile);
        if (!File.Exists(path))
        {
            Console.WriteLine($"No native knowledge yet. Run `iaet native analyze --project {project}` first.");
            return null;
        }

        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("binaries", out var arr))
                    return [];
                return arr.Deserialize<List<NativeBinaryInfo>>(JsonOptions) ?? [];
            }
        }
    }

    /// <summary>Appends new unknowns to knowledge/unknowns.json without disturbing entries already resolved by hand.</summary>
    internal static async Task<int> MergeUnknownsAsync(string path, IEnumerable<ResidualUnknown> fresh, CancellationToken ct)
    {
        var existing = new List<ResidualUnknown>();
        if (File.Exists(path))
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("unknowns", out var arr))
                    existing = arr.Deserialize<List<ResidualUnknown>>(JsonOptions) ?? [];
            }
        }

        var known = existing.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var added = 0;
        foreach (var u in fresh)
        {
            if (known.Add(u.Id))
            {
                existing.Add(u);
                added++;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
        {
            updatedAt = DateTimeOffset.UtcNow,
            open = existing.Count(u => u.Status == UnknownStatus.Open),
            unknowns = existing,
        }, JsonOptions), ct).ConfigureAwait(false);
        return added;
    }

    internal static string BuildJniDiagram(string project, IReadOnlyList<(string Library, JniBinding Binding)> bindings)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"---\ntitle: {project} — Java ↔ native (JNI) bridge\n---");
        sb.AppendLine("graph LR");
        var classIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var libIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (lib, b) in bindings.Take(60))
        {
            if (!classIds.TryGetValue(b.JavaClass, out var cid))
            {
                cid = $"J{classIds.Count}";
                classIds[b.JavaClass] = cid;
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {cid}[\"{b.JavaClass}\"]");
            }
            if (!libIds.TryGetValue(lib, out var lid))
            {
                lid = $"N{libIds.Count}";
                libIds[lib] = lid;
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {lid}[(\"{lib}\")]");
            }
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {cid} -->|{b.JavaMethod}| {lid}");
        }
        if (bindings.Count > 60)
            sb.AppendLine(CultureInfo.InvariantCulture, $"    More[\"... {bindings.Count - 60} more bindings in knowledge/native-binaries.json\"]");
        return sb.ToString();
    }
}
