using System.Text.Json;

namespace Iaet.Core.Abstractions;

public interface IKnowledgeStore
{
    Task<JsonDocument?> ReadAsync(string projectName, string fileName, CancellationToken ct = default);
    Task WriteAsync(string projectName, string fileName, JsonDocument content, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListFilesAsync(string projectName, CancellationToken ct = default);
}
