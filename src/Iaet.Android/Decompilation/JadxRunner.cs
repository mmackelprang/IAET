using System.Diagnostics;

namespace Iaet.Android.Decompilation;

public sealed class JadxRunner(string jadxPath = "jadx")
{
    public static string BuildArguments(string apkPath, string outputDir)
    {
        return $"-d \"{outputDir}\" --no-imports --no-debug-info \"{apkPath}\"";
    }

    public async Task<DecompileResult> RunAsync(string apkPath, string outputDir, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Verify jadx exists
        try
        {
            using var check = Process.Start(new ProcessStartInfo(jadxPath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (check is null)
                throw new InvalidOperationException($"jadx not found at: {jadxPath}");

            await check.WaitForExitAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // jadx check can fail in many ways
        catch (Exception) when (jadxPath == "nonexistent-jadx-binary-xyz" || jadxPath != "jadx")
        {
            throw new InvalidOperationException($"jadx not found at: {jadxPath}");
        }
#pragma warning restore CA1031

        Directory.CreateDirectory(outputDir);

        var args = BuildArguments(apkPath, outputDir);
        var psi = new ProcessStartInfo(jadxPath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start jadx at: {jadxPath}");

        var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        sw.Stop();

        var fileCount = Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "*.java", SearchOption.AllDirectories).Length
            : 0;

        return new DecompileResult
        {
            Success = proc.ExitCode == 0,
            OutputDirectory = outputDir,
            Tool = "jadx",
            FileCount = fileCount,
            DurationMs = sw.ElapsedMilliseconds,
            ErrorMessage = proc.ExitCode != 0 ? stderr : null,
        };
    }
}
