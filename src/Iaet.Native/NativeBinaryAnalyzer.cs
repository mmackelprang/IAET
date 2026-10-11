using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Models;
using Iaet.Native.Elf;
using Iaet.Native.Extractors;
using Iaet.Native.Ghidra;

namespace Iaet.Native;

public sealed record NativeAnalysisOptions
{
    /// <summary>Explicit analyzeHeadless path; null means auto-locate via <see cref="GhidraLocator"/>.</summary>
    public string? GhidraPath { get; init; }

    /// <summary>Run Ghidra when available. Static header/strings analysis always runs.</summary>
    public bool UseGhidra { get; init; } = true;

    public bool Decompile { get; init; }
    public int MaxFunctions { get; init; } = 2000;
    public bool UseCache { get; init; } = true;
    public int MinStringLength { get; init; } = 5;

    /// <summary>Scratch directory for Ghidra projects and exports (defaults to <c>&lt;project&gt;/native/work</c>).</summary>
    public string? WorkDirectory { get; init; }
}

/// <summary>
/// Orchestrates native binary analysis: cheap, always-on static providers (format detection,
/// ELF symbol tables, strings, artifact extraction, library fingerprints) plus the optional
/// Ghidra headless provider. Each provider's contribution is recorded as <see cref="Evidence"/>;
/// what could not be established becomes a <see cref="ResidualUnknown"/>.
/// </summary>
public static class NativeBinaryAnalyzer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string[] PreferredAbiOrder = ["arm64-v8a", "armeabi-v7a", "x86_64", "x86", "armeabi"];

    /// <summary>
    /// Finds native libraries belonging to a decompiled APK. jadx writes resources under
    /// <c>decompiled/resources/lib/&lt;abi&gt;/*.so</c>; apktool under <c>resources/lib/&lt;abi&gt;/*.so</c>.
    /// Only the most capable ABI is returned per library name so each is analyzed once.
    /// </summary>
    public static IReadOnlyList<(string Path, string Abi)> DiscoverApkLibraries(string apkDir)
    {
        ArgumentNullException.ThrowIfNull(apkDir);
        var candidates = new List<(string Path, string Abi)>();
        foreach (var libRoot in new[] { Path.Combine(apkDir, "decompiled", "resources", "lib"), Path.Combine(apkDir, "resources", "lib"), Path.Combine(apkDir, "lib") })
        {
            if (!Directory.Exists(libRoot))
                continue;
            foreach (var abiDir in Directory.EnumerateDirectories(libRoot))
            {
                var abi = Path.GetFileName(abiDir);
                foreach (var so in Directory.EnumerateFiles(abiDir, "*.so", SearchOption.TopDirectoryOnly))
                    candidates.Add((so, abi));
            }
        }

        return candidates
            .GroupBy(c => Path.GetFileName(c.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(c => AbiRank(c.Abi)).First())
            .OrderBy(c => Path.GetFileName(c.Path), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int AbiRank(string abi)
    {
        var idx = Array.IndexOf(PreferredAbiOrder, abi);
        return idx < 0 ? PreferredAbiOrder.Length : idx;
    }

    public static async Task<NativeBinaryInfo> AnalyzeAsync(string binaryPath, string projectRelativePath, string? abi, NativeAnalysisOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binaryPath);
        ArgumentNullException.ThrowIfNull(options);

        var bytes = await File.ReadAllBytesAsync(binaryPath, ct).ConfigureAwait(false);
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var evidence = new List<Evidence>();
        var unknowns = new List<ResidualUnknown>();
        var limitations = new List<string>();
        var providers = new List<string>();
        var fileName = Path.GetFileName(binaryPath);

        var detection = BinaryFormatDetector.Detect(bytes);
        providers.Add("format-detector");
        evidence.Add(new Evidence
        {
            Kind = EvidenceKind.Derivation,
            Provider = "format-detector",
            Artifact = projectRelativePath,
            ArtifactSha256 = sha,
            Location = "header",
            Confidence = detection.Format == BinaryFormat.Unknown ? ConfidenceLevel.Low : ConfidenceLevel.High,
            Limitations = detection.Format == BinaryFormat.Unknown ? ["Magic bytes not recognized — may be packed, encrypted, or not a native image"] : [],
        });

        ElfInfo? elf = null;
        if (detection.Format == BinaryFormat.Elf)
        {
            try
            {
                elf = ElfReader.Parse(bytes);
                providers.Add("elf-reader");
                evidence.Add(new Evidence
                {
                    Kind = EvidenceKind.Observation,
                    Provider = "elf-reader",
                    Artifact = projectRelativePath,
                    ArtifactSha256 = sha,
                    Location = ".dynsym/.dynamic",
                    Confidence = ConfidenceLevel.High,
                    Limitations = elf.Warnings,
                });
                limitations.AddRange(elf.Warnings);
            }
            catch (InvalidDataException ex)
            {
                limitations.Add($"ELF parse failed: {ex.Message}");
                unknowns.Add(new ResidualUnknown
                {
                    Id = $"elf-parse-{fileName}",
                    Question = $"Why does {fileName} fail ELF parsing? (packed, truncated, or non-standard layout)",
                    Source = "elf-reader",
                    Context = projectRelativePath,
                    SuggestedAction = "Run `iaet native analyze --ghidra-path <dir>` so Ghidra's loaders can attempt the import, or inspect with `readelf -h`.",
                });
            }
        }
        else if (detection.Format is BinaryFormat.Pe or BinaryFormat.MachO or BinaryFormat.MachOFat)
        {
            limitations.Add($"{detection.Format} symbol tables are not parsed statically; strings and (with Ghidra) functions are still available.");
        }

        var strings = BinaryStringExtractor.Extract(bytes, options.MinStringLength);
        providers.Add("strings");
        var urls = NativeArtifactExtractor.ExtractUrls(strings, projectRelativePath);
        var uuids = NativeArtifactExtractor.ExtractBleUuids(strings);
        var symbols = (elf?.Imports ?? []).Concat(elf?.Exports ?? []).ToList();
        var libraries = LibraryFingerprinter.Fingerprint(strings, symbols);
        var jni = NativeArtifactExtractor.ExtractJniBindings(elf?.Exports ?? []);

        if (urls.Count > 0)
        {
            evidence.Add(new Evidence
            {
                Kind = EvidenceKind.Observation,
                Provider = "strings",
                Artifact = projectRelativePath,
                ArtifactSha256 = sha,
                Location = "string table",
                Confidence = ConfidenceLevel.Medium,
                Limitations = ["URL literals prove the host is referenced, not that it is contacted at runtime", "Format-string URLs need runtime substitution"],
            });
        }
        foreach (var lib in libraries)
        {
            evidence.Add(new Evidence
            {
                Kind = EvidenceKind.Inference,
                Provider = "library-fingerprinter",
                Artifact = projectRelativePath,
                ArtifactSha256 = sha,
                Location = string.Join(',', lib.MatchedIndicators.Take(3)),
                Confidence = lib.Confidence,
                Limitations = ["Fingerprints match embedded strings/symbols; a vendored or modified copy may differ from the upstream library"],
            });
        }

        if (jni.Count == 0 && elf is not null && elf.Exports.Count > 0 && abi is not null)
        {
            // Java can still reach the library via RegisterNatives; the symbol table alone cannot tell.
            unknowns.Add(new ResidualUnknown
            {
                Id = $"jni-registration-{fileName}",
                Question = $"{fileName} exports no Java_* symbols — does it register natives dynamically (RegisterNatives) or is it only used by other native libs?",
                Source = "elf-reader",
                Context = projectRelativePath,
                SuggestedAction = "Search decompiled Java for System.loadLibrary(\"" + fileName.Replace("lib", string.Empty, StringComparison.Ordinal).Replace(".so", string.Empty, StringComparison.Ordinal) + "\") and `native` method declarations; run `iaet native search --query RegisterNatives`.",
            });
        }

        var functions = new List<NativeFunction>();
        if (options.UseGhidra)
        {
            var launcher = GhidraLocator.FindAnalyzeHeadless(options.GhidraPath);
            if (launcher is null)
            {
                limitations.Add("Ghidra not available — functions, call graph and pseudocode not recovered.");
                unknowns.Add(new ResidualUnknown
                {
                    Id = $"ghidra-missing-{fileName}",
                    Question = $"What does the native code in {fileName} actually do? (no decompiler ran)",
                    Source = "ghidra",
                    Context = projectRelativePath,
                    SuggestedAction = "Install Ghidra, set GHIDRA_INSTALL_DIR, and re-run `iaet native analyze --project <name> --decompile`.",
                });
            }
            else
            {
                var ghidra = await RunGhidraAsync(launcher, binaryPath, sha, options, ct).ConfigureAwait(false);
                if (ghidra.Export is not null)
                {
                    providers.Add("ghidra");
                    functions.AddRange(ghidra.Export.Functions);
                    evidence.Add(new Evidence
                    {
                        Kind = EvidenceKind.Derivation,
                        Provider = "ghidra",
                        Artifact = projectRelativePath,
                        ArtifactSha256 = sha,
                        Location = $"language={ghidra.Export.LanguageId} imageBase={ghidra.Export.ImageBase}",
                        Confidence = ConfidenceLevel.High,
                        Limitations = BuildGhidraLimitations(ghidra.Export, options, ghidra.FromCache),
                    });
                    if (ghidra.Export.Truncated)
                        limitations.Add($"Ghidra export truncated to {options.MaxFunctions} of {ghidra.Export.FunctionCount} functions (raise --max-functions).");
                    if (!options.Decompile)
                        limitations.Add("Pseudocode not exported — re-run with --decompile to include it.");
                }
                else
                {
                    limitations.Add($"Ghidra run failed: {ghidra.Error}");
                    unknowns.Add(new ResidualUnknown
                    {
                        Id = $"ghidra-failed-{fileName}",
                        Question = $"Ghidra could not analyze {fileName}: {ghidra.Error}",
                        Source = "ghidra",
                        Context = projectRelativePath,
                        SuggestedAction = "Check the log excerpt in native/work, try `--language <ghidra language id>`, or raise the timeout.",
                    });
                }
            }
        }

        return new NativeBinaryInfo
        {
            Path = projectRelativePath,
            Sha256 = sha,
            SizeBytes = bytes.LongLength,
            Format = detection.Format,
            Architecture = elf?.Architecture ?? detection.Architecture,
            Bitness = elf?.Bitness ?? detection.Bitness,
            LittleEndian = elf?.LittleEndian ?? detection.LittleEndian,
            Abi = abi,
            NeededLibraries = elf?.NeededLibraries ?? [],
            Imports = elf?.Imports ?? [],
            Exports = elf?.Exports ?? [],
            JniBindings = jni,
            Urls = urls,
            BleUuids = uuids,
            Libraries = libraries,
            StringCount = strings.Count,
            Functions = functions,
            Providers = providers,
            Evidence = evidence,
            Unknowns = unknowns,
            Limitations = limitations,
        };
    }

    private static List<string> BuildGhidraLimitations(GhidraExport export, NativeAnalysisOptions options, bool fromCache)
    {
        var list = new List<string> { "Static analysis — call graph edges are resolved calls only; indirect/virtual calls are not followed" };
        if (export.Truncated)
            list.Add(string.Create(CultureInfo.InvariantCulture, $"Function list truncated to {options.MaxFunctions}"));
        if (fromCache)
            list.Add("Served from snapshot cache (same bytes, options and script version)");
        return list;
    }

    private sealed record GhidraOutcome(GhidraExport? Export, bool FromCache, string? Error);

    private static async Task<GhidraOutcome> RunGhidraAsync(string launcher, string binaryPath, string sha, NativeAnalysisOptions options, CancellationToken ct)
    {
        var workDir = options.WorkDirectory ?? Path.Combine(Path.GetTempPath(), "iaet-native");
        var cache = new NativeAnalysisCache(Path.Combine(workDir, "cache"));
        var runOptions = new GhidraRunOptions { Decompile = options.Decompile, MaxFunctions = options.MaxFunctions };
        var key = NativeAnalysisCache.ComputeKey(sha, "ghidra-export", GhidraHeadlessRunner.ScriptVersion, runOptions.ParameterKey());

        if (options.UseCache && cache.TryGet(key, out var cached))
        {
            try
            {
                return new GhidraOutcome(GhidraExportParser.Parse(cached), FromCache: true, null);
            }
            catch (JsonException)
            {
                // Corrupt cache entry — fall through and regenerate.
            }
        }

        var runner = new GhidraHeadlessRunner(launcher);
        var result = await runner.RunAsync(binaryPath, Path.Combine(workDir, "work", sha[..12]), runOptions, ct).ConfigureAwait(false);
        if (!result.Success)
            return new GhidraOutcome(null, false, result.ErrorMessage ?? "unknown error");

        var json = await File.ReadAllTextAsync(result.OutputJsonPath, ct).ConfigureAwait(false);
        GhidraExport export;
        try
        {
            export = GhidraExportParser.Parse(json);
        }
        catch (JsonException ex)
        {
            return new GhidraOutcome(null, false, $"export JSON unreadable: {ex.Message}");
        }

        if (options.UseCache)
            await cache.PutAsync(key, json, ct).ConfigureAwait(false);
        return new GhidraOutcome(export, false, null);
    }
}
