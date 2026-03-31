using Iaet.Core.Models;

namespace Iaet.Cookies;

public static class CookieLifecycleAnalyzer
{
    private static readonly TimeSpan DefaultExpiryThreshold = TimeSpan.FromHours(1);

    public static CookieAnalysis Analyze(
        string projectName,
        CookieSnapshotInfo snapshot,
        TimeSpan? expiryThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var threshold = expiryThreshold ?? DefaultExpiryThreshold;
        var now = DateTimeOffset.UtcNow;
        var expiring = new Dictionary<string, TimeSpan>();

        foreach (var cookie in snapshot.Cookies)
        {
            if (cookie.Expires.HasValue)
            {
                var remaining = cookie.Expires.Value - now;
                if (remaining > TimeSpan.Zero && remaining <= threshold)
                {
                    expiring[cookie.Name] = remaining;
                }
            }
        }

        return new CookieAnalysis
        {
            ProjectName = projectName,
            TotalCookies = snapshot.Cookies.Count,
            ExpiringWithin = expiring,
        };
    }

    public static CookieAnalysis AnalyzeRotation(
        string projectName,
        IReadOnlyList<CookieSnapshotInfo> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count < 2)
            return new CookieAnalysis { ProjectName = projectName, TotalCookies = 0 };

        var rotated = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var diff = CookieDiffer.Diff(snapshots[i - 1], snapshots[i]);
            foreach (var change in diff.Changed)
            {
                rotated.Add(change.Name);
            }
        }

        var latest = snapshots[^1];
        return new CookieAnalysis
        {
            ProjectName = projectName,
            TotalCookies = latest.Cookies.Count,
            RotationDetected = rotated.ToList(),
        };
    }
}
