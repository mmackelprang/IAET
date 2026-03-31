using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Projects;

public sealed class RoundStore : IRoundStore
{
    private readonly string _rootDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public RoundStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public async Task<int> CreateRoundAsync(string projectName, RoundPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var roundDir = GetRoundDirectory(projectName, plan.RoundNumber);
        Directory.CreateDirectory(roundDir);

        var planPath = Path.Combine(roundDir, "plan.json");
        await WriteJsonAsync(planPath, plan, ct).ConfigureAwait(false);
        return plan.RoundNumber;
    }

    public async Task<RoundPlan?> GetPlanAsync(string projectName, int roundNumber, CancellationToken ct = default)
    {
        var planPath = Path.Combine(GetRoundDirectory(projectName, roundNumber), "plan.json");
        if (!File.Exists(planPath))
            return null;

        var stream = File.OpenRead(planPath);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<RoundPlan>(stream, JsonOptions, ct).ConfigureAwait(false);
        }
    }

    public async Task SaveFindingsAsync(string projectName, int roundNumber, AgentFindings findings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(findings);

        var roundDir = GetRoundDirectory(projectName, roundNumber);
        var findingsPath = Path.Combine(roundDir, $"findings-{findings.Agent}.json");
        await WriteJsonAsync(findingsPath, findings, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentFindings>> GetFindingsAsync(string projectName, int roundNumber, CancellationToken ct = default)
    {
        var roundDir = GetRoundDirectory(projectName, roundNumber);
        if (!Directory.Exists(roundDir))
            return [];

        var results = new List<AgentFindings>();
        foreach (var file in Directory.EnumerateFiles(roundDir, "findings-*.json"))
        {
            var stream = File.OpenRead(file);
            await using (stream.ConfigureAwait(false))
            {
                var findings = await JsonSerializer.DeserializeAsync<AgentFindings>(stream, JsonOptions, ct).ConfigureAwait(false);
                if (findings is not null)
                    results.Add(findings);
            }
        }
        return results;
    }

    private string GetRoundDirectory(string projectName, int roundNumber) =>
        Path.Combine(_rootDirectory, projectName, "rounds", $"{roundNumber:D3}-round");

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken ct)
    {
        var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, ct).ConfigureAwait(false);
        }
    }
}
