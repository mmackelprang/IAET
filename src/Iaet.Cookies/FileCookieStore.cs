using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Cookies;

public sealed class FileCookieStore(string rootDirectory) : ICookieStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task SaveSnapshotAsync(CookieSnapshotInfo snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var dir = GetSnapshotDir(snapshot.ProjectName);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{snapshot.Id:N}.json");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    public async Task<CookieSnapshotInfo?> GetSnapshotAsync(string projectName, Guid snapshotId, CancellationToken ct = default)
    {
        var path = Path.Combine(GetSnapshotDir(projectName), $"{snapshotId:N}.json");
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<CookieSnapshotInfo>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<CookieSnapshotInfo>> ListSnapshotsAsync(string projectName, CancellationToken ct = default)
    {
        var dir = GetSnapshotDir(projectName);
        if (!Directory.Exists(dir))
            return [];

        var results = new List<CookieSnapshotInfo>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<CookieSnapshotInfo>(json, JsonOptions);
            if (snapshot is not null)
                results.Add(snapshot);
        }
        return results;
    }

    private string GetSnapshotDir(string projectName) =>
        Path.Combine(rootDirectory, projectName, "cookies");
}
