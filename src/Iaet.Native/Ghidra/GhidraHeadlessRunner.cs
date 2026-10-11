using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Iaet.Native.Ghidra;

public sealed record GhidraRunOptions
{
    public bool Decompile { get; init; }
    public int MaxFunctions { get; init; } = 2000;
    public int MaxPseudocodeChars { get; init; } = 20_000;

    /// <summary>Per-file analysis timeout passed to Ghidra (<c>-analysisTimeoutPerFile</c>), in seconds.</summary>
    public int AnalysisTimeoutSeconds { get; init; } = 600;

    /// <summary>Overall wall-clock budget for the headless process.</summary>
    public TimeSpan ProcessTimeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Optional Ghidra language id (e.g. <c>ARM:LE:32:v8</c>) when auto-detection picks the wrong processor.</summary>
    public string? LanguageId { get; init; }

    public string ParameterKey() => string.Create(CultureInfo.InvariantCulture,
        $"decompile={Decompile};maxFunctions={MaxFunctions};maxPseudocodeChars={MaxPseudocodeChars};lang={LanguageId}");
}

public sealed record GhidraRunResult
{
    public required bool Success { get; init; }
    public required string OutputJsonPath { get; init; }
    public long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? LogExcerpt { get; init; }
}

/// <summary>
/// Drives Ghidra's headless analyzer against one binary and collects the JSON written by the
/// packaged <c>IaetGhidraExport.java</c> post-script. The project is created in a scratch
/// directory and deleted afterwards (<c>-deleteProject</c>), and the input file is never modified.
/// </summary>
public sealed class GhidraHeadlessRunner(string analyzeHeadlessPath)
{
    public const string ScriptFileName = "IaetGhidraExport.java";
    public const string ScriptVersion = "1";
    private const string ResourceName = "Iaet.Native.Ghidra.IaetGhidraExport.java";

    public string AnalyzeHeadlessPath { get; } = analyzeHeadlessPath;

    public static IReadOnlyList<string> BuildArguments(string projectDir, string projectName, string binaryPath, string scriptDir, string outputJson, GhidraRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var args = new List<string>
        {
            projectDir,
            projectName,
            "-import", binaryPath,
            "-deleteProject",
            "-analysisTimeoutPerFile", options.AnalysisTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            "-scriptPath", scriptDir,
            "-postScript", ScriptFileName,
            outputJson,
            string.Create(CultureInfo.InvariantCulture, $"decompile={(options.Decompile ? "true" : "false")}"),
            string.Create(CultureInfo.InvariantCulture, $"maxFunctions={options.MaxFunctions}"),
            string.Create(CultureInfo.InvariantCulture, $"maxPseudocodeChars={options.MaxPseudocodeChars}"),
        };
        if (!string.IsNullOrWhiteSpace(options.LanguageId))
        {
            args.Add("-processor");
            args.Add(options.LanguageId);
        }
        return args;
    }

    /// <summary>Writes the embedded post-script into <paramref name="scriptDir"/> and returns its path.</summary>
    public static async Task<string> MaterializeScriptAsync(string scriptDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(scriptDir);
        var target = Path.Combine(scriptDir, ScriptFileName);
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        await using (stream.ConfigureAwait(false))
        {
            var file = File.Create(target);
            await using (file.ConfigureAwait(false))
            {
                await stream.CopyToAsync(file, ct).ConfigureAwait(false);
            }
        }
        return target;
    }

    public async Task<GhidraRunResult> RunAsync(string binaryPath, string workDir, GhidraRunOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sw = Stopwatch.StartNew();

        Directory.CreateDirectory(workDir);
        var projectDir = Path.Combine(workDir, "ghidra-project");
        var scriptDir = Path.Combine(workDir, "ghidra-scripts");
        var outputJson = Path.Combine(workDir, Path.GetFileName(binaryPath) + ".ghidra.json");
        // A killed or crashed earlier run leaves a locked project behind; start clean so -import never
        // collides with an existing program.
        if (Directory.Exists(projectDir))
            Directory.Delete(projectDir, recursive: true);
        Directory.CreateDirectory(projectDir);
        await MaterializeScriptAsync(scriptDir, ct).ConfigureAwait(false);
        if (File.Exists(outputJson))
            File.Delete(outputJson);

        var psi = new ProcessStartInfo(AnalyzeHeadlessPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir,
        };
        foreach (var a in BuildArguments(projectDir, "iaet", binaryPath, scriptDir, outputJson, options))
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start Ghidra at: {AnalyzeHeadlessPath}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(options.ProcessTimeout);

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = proc.StandardError.ReadToEndAsync(timeoutCts.Token);
        string stdout = string.Empty, stderr = string.Empty;
        var timedOut = false;
        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            stdout = await stdoutTask.ConfigureAwait(false);
            stderr = await stderrTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Timeout or caller cancellation: never leave a headless JVM running against the scratch project.
            try { proc.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already exited */ }

            if (ct.IsCancellationRequested)
                throw;
            timedOut = true;
        }

        sw.Stop();
        var excerpt = Tail(stdout + Environment.NewLine + stderr, 4000);

        if (timedOut)
        {
            return new GhidraRunResult
            {
                Success = false,
                OutputJsonPath = outputJson,
                DurationMs = sw.ElapsedMilliseconds,
                ErrorMessage = $"Ghidra headless run exceeded {options.ProcessTimeout.TotalMinutes:0} minutes and was terminated.",
                LogExcerpt = excerpt,
            };
        }

        var ok = proc.ExitCode == 0 && File.Exists(outputJson);
        return new GhidraRunResult
        {
            Success = ok,
            OutputJsonPath = outputJson,
            DurationMs = sw.ElapsedMilliseconds,
            ErrorMessage = ok ? null : $"analyzeHeadless exited with code {proc.ExitCode}" + (File.Exists(outputJson) ? string.Empty : " and produced no export file"),
            LogExcerpt = excerpt,
        };
    }

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];
}
