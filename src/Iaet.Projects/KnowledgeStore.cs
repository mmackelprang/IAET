using System.Text.Json;
using Iaet.Core.Abstractions;

namespace Iaet.Projects;

public sealed class KnowledgeStore : IKnowledgeStore
{
    private readonly string _rootDirectory;

    public KnowledgeStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public async Task<JsonDocument?> ReadAsync(string projectName, string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(_rootDirectory, projectName, "knowledge", fileName);
        if (!File.Exists(path))
            return null;

        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        }
    }

    public async Task WriteAsync(string projectName, string fileName, JsonDocument content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var dir = Path.Combine(_rootDirectory, projectName, "knowledge");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);

        var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (stream.ConfigureAwait(false))
        {
            var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            await using (writer.ConfigureAwait(false))
            {
                content.WriteTo(writer);
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }
        }
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string projectName, CancellationToken ct = default)
    {
        var dir = Path.Combine(_rootDirectory, projectName, "knowledge");
        if (!Directory.Exists(dir))
            return Task.FromResult<IReadOnlyList<string>>([]);

        var files = Directory.EnumerateFiles(dir, "*.json")
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }
}
