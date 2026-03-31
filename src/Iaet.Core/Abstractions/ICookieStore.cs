using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ICookieStore
{
    Task SaveSnapshotAsync(CookieSnapshotInfo snapshot, CancellationToken ct = default);
    Task<CookieSnapshotInfo?> GetSnapshotAsync(string projectName, Guid snapshotId, CancellationToken ct = default);
    Task<IReadOnlyList<CookieSnapshotInfo>> ListSnapshotsAsync(string projectName, CancellationToken ct = default);
}
