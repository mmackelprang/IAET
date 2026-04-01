using System.Diagnostics;

namespace Iaet.Android.Decompilation;

public sealed class ApktoolRunner(string apktoolPath = "apktool")
{
    public static string BuildArguments(string apkPath, string outputDir)
    {
        return $"d -o \"{outputDir}\" -f \"{apkPath}\"";
    }

    public async Task<DecompileResult> RunAsync(string apkPath, string outputDir, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var check = Process.Start(new ProcessStartInfo(apktoolPath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (check is null)
                throw new InvalidOperationException($"apktool not found at: {apktoolPath}");

            await check.WaitForExitAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception) when (apktoolPath != "apktool")
        {
            throw new InvalidOperationException($"apktool not found at: {apktoolPath}");
        }
#pragma warning restore CA1031

        Directory.CreateDirectory(outputDir);

        var args = BuildArguments(apkPath, outputDir);
        var psi = new ProcessStartInfo(apktoolPath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start apktool at: {apktoolPath}");

        var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        sw.Stop();

        return new DecompileResult
        {
            Success = proc.ExitCode == 0,
            OutputDirectory = outputDir,
            Tool = "apktool",
            DurationMs = sw.ElapsedMilliseconds,
            ErrorMessage = proc.ExitCode != 0 ? stderr : null,
        };
    }
}
