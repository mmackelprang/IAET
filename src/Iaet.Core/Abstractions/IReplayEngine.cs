using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IReplayEngine
{
    Task<ReplayResult> ReplayAsync(CapturedRequest original, CancellationToken ct = default);
}
