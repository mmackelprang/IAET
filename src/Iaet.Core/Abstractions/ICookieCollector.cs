using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ICookieCollector
{
    Task<IReadOnlyList<CapturedCookie>> CollectAllAsync(CancellationToken ct = default);
    Task<CookieSnapshotInfo> TakeSnapshotAsync(string projectName, string source, CancellationToken ct = default);
}
