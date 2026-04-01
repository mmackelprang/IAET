using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Projects;

public sealed class ProjectStore : IProjectStore
{
    private readonly string _rootDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public ProjectStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string GetProjectDirectory(string projectName) =>
        Path.Combine(_rootDirectory, projectName);

    public async Task<ProjectConfig> CreateAsync(ProjectConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var dir = GetProjectDirectory(config.Name);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "rounds"));
        Directory.CreateDirectory(Path.Combine(dir, "output", "diagrams"));
        Directory.CreateDirectory(Path.Combine(dir, "knowledge"));

        var path = Path.Combine(dir, "project.json");
        await WriteJsonAsync(path, config, ct).ConfigureAwait(false);
        return config;
    }

    public async Task<ProjectConfig?> LoadAsync(string projectName, CancellationToken ct = default)
    {
        var path = Path.Combine(GetProjectDirectory(projectName), "project.json");
        if (!File.Exists(path))
            return null;

        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<ProjectConfig>(stream, JsonOptions, ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ProjectConfig>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_rootDirectory))
            return [];

        var results = new List<ProjectConfig>();
        foreach (var dir in Directory.EnumerateDirectories(_rootDirectory))
        {
            var projectFile = Path.Combine(dir, "project.json");
            if (!File.Exists(projectFile))
                continue;

            var stream = File.OpenRead(projectFile);
            await using (stream.ConfigureAwait(false))
            {
                var config = await JsonSerializer.DeserializeAsync<ProjectConfig>(stream, JsonOptions, ct).ConfigureAwait(false);
                if (config is not null)
                    results.Add(config);
            }
        }
        return results;
    }

    public async Task SaveAsync(ProjectConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var path = Path.Combine(GetProjectDirectory(config.Name), "project.json");
        await WriteJsonAsync(path, config, ct).ConfigureAwait(false);
    }

    public async Task ArchiveAsync(string projectName, CancellationToken ct = default)
    {
        var config = await LoadAsync(projectName, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Project '{projectName}' not found.");
        var archived = config with { Status = ProjectStatus.Archived };
        await SaveAsync(archived, ct).ConfigureAwait(false);
    }

    public async Task<ProjectConfig> RefreshStatusAsync(string projectName, CancellationToken ct = default)
    {
        var config = await LoadAsync(projectName, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Project '{projectName}' not found.");

        if (config.Status == ProjectStatus.Archived || config.Status == ProjectStatus.Complete)
            return config;

        var dir = GetProjectDirectory(projectName);
        var knowledgeDir = Path.Combine(dir, "knowledge");
        var capturesDir = Path.Combine(dir, "captures");
        var roundsDir = Path.Combine(dir, "rounds");
        var outputDir = Path.Combine(dir, "output");

        var hasKnowledge = Directory.Exists(knowledgeDir) && Directory.EnumerateFiles(knowledgeDir, "*.json").Any();
        var hasCaptures = Directory.Exists(capturesDir) && Directory.EnumerateFiles(capturesDir).Any();
        var hasRounds = Directory.Exists(roundsDir) && Directory.EnumerateDirectories(roundsDir).Any();
        var hasOutput = Directory.Exists(outputDir) && Directory.EnumerateFiles(outputDir).Any();
        var hasDecompiled = Directory.Exists(Path.Combine(dir, "apk", "decompiled"));

        var newStatus = config.Status;
        if (hasKnowledge || hasCaptures || hasRounds || hasOutput || hasDecompiled)
            newStatus = ProjectStatus.Investigating;

        if (newStatus != config.Status)
        {
            var updated = config with { Status = newStatus, LastActivityAt = DateTimeOffset.UtcNow };
            await SaveAsync(updated, ct).ConfigureAwait(false);
            return updated;
        }

        return config;
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken ct)
    {
        var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, ct).ConfigureAwait(false);
        }
    }
}
