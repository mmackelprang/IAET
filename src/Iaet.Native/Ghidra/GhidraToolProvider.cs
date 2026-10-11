using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Native.Ghidra;

/// <summary>Probes for a Ghidra installation so <c>iaet doctor</c> and <c>iaet native analyze</c> can report it.</summary>
public sealed class GhidraToolProvider : IToolProvider
{
    public string Name => "ghidra";

    public IReadOnlyList<string> Capabilities { get; } = ["native analyze --decompile", "native function", "native search (function names)"];

    public async Task<ToolHealth> CheckAsync(CancellationToken ct = default)
    {
        var launcher = GhidraLocator.FindAnalyzeHeadless();
        if (launcher is null)
        {
            return new ToolHealth
            {
                Name = Name,
                Capabilities = Capabilities,
                Available = false,
                Message = "Ghidra not found. Install from https://github.com/NationalSecurityAgency/ghidra/releases and set GHIDRA_INSTALL_DIR (or IAET_GHIDRA_PATH) to the install directory. Requires a JDK 21+.",
            };
        }

        string? version = null;
        var root = Path.GetDirectoryName(Path.GetDirectoryName(launcher));
        if (root is not null)
        {
            var props = Path.Combine(root, "Ghidra", "application.properties");
            if (File.Exists(props))
            {
                foreach (var line in await File.ReadAllLinesAsync(props, ct).ConfigureAwait(false))
                {
                    if (line.StartsWith("application.version=", StringComparison.Ordinal))
                    {
                        version = line["application.version=".Length..].Trim();
                        break;
                    }
                }
            }
        }

        return new ToolHealth
        {
            Name = Name,
            Capabilities = Capabilities,
            Available = true,
            ResolvedPath = launcher,
            Version = version,
            Message = "Headless analyzer available. Static ELF/strings analysis works without it; decompilation and call graphs need it.",
        };
    }
}
