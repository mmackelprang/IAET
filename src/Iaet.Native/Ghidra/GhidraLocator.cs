namespace Iaet.Native.Ghidra;

/// <summary>
/// Finds a Ghidra installation. Resolution order: explicit path, <c>IAET_GHIDRA_PATH</c>,
/// <c>GHIDRA_INSTALL_DIR</c>, <c>GHIDRA_HOME</c>, then <c>analyzeHeadless</c> on PATH.
/// Ghidra is bring-your-own: IAET never downloads or installs it.
/// </summary>
public static class GhidraLocator
{
    public static readonly string[] EnvironmentVariables = ["IAET_GHIDRA_PATH", "GHIDRA_INSTALL_DIR", "GHIDRA_HOME"];

    /// <summary>Returns the path of the analyzeHeadless launcher, or null when Ghidra cannot be found.</summary>
    public static string? FindAnalyzeHeadless(string? explicitPath = null, Func<string, string?>? getEnv = null, string? pathVariable = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;

        if (!string.IsNullOrWhiteSpace(explicitPath))
            return ResolveLauncher(explicitPath);

        foreach (var variable in EnvironmentVariables)
        {
            var value = getEnv(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                var resolved = ResolveLauncher(value);
                if (resolved is not null)
                    return resolved;
            }
        }

        pathVariable ??= getEnv("PATH");
        if (string.IsNullOrEmpty(pathVariable))
            return null;

        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in LauncherNames())
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }

    /// <summary>Accepts a launcher file, a Ghidra root, or a <c>support/</c> directory and returns the launcher.</summary>
    public static string? ResolveLauncher(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (File.Exists(path))
            return path;

        if (!Directory.Exists(path))
            return null;

        foreach (var name in LauncherNames())
        {
            var direct = Path.Combine(path, name);
            if (File.Exists(direct))
                return direct;

            var inSupport = Path.Combine(path, "support", name);
            if (File.Exists(inSupport))
                return inSupport;
        }
        return null;
    }

    private static IEnumerable<string> LauncherNames() =>
        OperatingSystem.IsWindows() ? ["analyzeHeadless.bat", "analyzeHeadless"] : ["analyzeHeadless", "analyzeHeadless.bat"];
}
