using System.Diagnostics;

namespace Iaet.Crawler;

public sealed class RecipeRunner
{
    public static void ValidateRecipe(string recipePath)
    {
        ArgumentNullException.ThrowIfNull(recipePath);
        if (!File.Exists(recipePath))
            throw new FileNotFoundException($"Recipe not found: {recipePath}", recipePath);
        if (!recipePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Recipe must be a TypeScript (.ts) file", nameof(recipePath));
    }

    public static (string Command, string Args) BuildCommand(string recipePath, int cdpPort)
    {
        ArgumentNullException.ThrowIfNull(recipePath);
        _ = cdpPort; // cdpPort is passed via environment variable
        return ("npx", $"tsx \"{recipePath}\"");
    }

    public static Dictionary<string, string> GetEnvironment(int cdpPort) => new()
    {
        ["CDP_ENDPOINT"] = $"ws://127.0.0.1:{cdpPort}"
    };

    public static async Task<int> RunAsync(string recipePath, int cdpPort, CancellationToken ct = default)
    {
        ValidateRecipe(recipePath);
        var (command, args) = BuildCommand(recipePath, cdpPort);
        var env = GetEnvironment(cdpPort);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };
        foreach (var (key, value) in env)
            process.StartInfo.EnvironmentVariables[key] = value;

        process.Start();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }
}
