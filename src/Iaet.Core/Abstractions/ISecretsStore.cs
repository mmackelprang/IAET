namespace Iaet.Core.Abstractions;

public interface ISecretsStore
{
    Task SetAsync(string projectName, string key, string value, CancellationToken ct = default);
    Task<string?> GetAsync(string projectName, string key, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> ListAsync(string projectName, CancellationToken ct = default);
    Task RemoveAsync(string projectName, string key, CancellationToken ct = default);
}
