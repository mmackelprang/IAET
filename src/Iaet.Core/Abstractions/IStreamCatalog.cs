using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IStreamCatalog
{
    Task SaveStreamAsync(CapturedStream stream, CancellationToken ct = default);
    Task<IReadOnlyList<CapturedStream>> GetStreamsBySessionAsync(Guid sessionId, CancellationToken ct = default);
}
