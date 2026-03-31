using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IProjectStore
{
    Task<ProjectConfig> CreateAsync(ProjectConfig config, CancellationToken ct = default);
    Task<ProjectConfig?> LoadAsync(string projectName, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectConfig>> ListAsync(CancellationToken ct = default);
    Task SaveAsync(ProjectConfig config, CancellationToken ct = default);
    Task ArchiveAsync(string projectName, CancellationToken ct = default);
    string GetProjectDirectory(string projectName);
}
