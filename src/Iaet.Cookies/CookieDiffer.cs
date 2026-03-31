using Iaet.Core.Models;

namespace Iaet.Cookies;

public static class CookieDiffer
{
    public static CookieDiff Diff(CookieSnapshotInfo before, CookieSnapshotInfo after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var beforeMap = before.Cookies.ToDictionary(CookieKey, StringComparer.Ordinal);
        var afterMap = after.Cookies.ToDictionary(CookieKey, StringComparer.Ordinal);

        var added = new List<CapturedCookie>();
        var removed = new List<CapturedCookie>();
        var changed = new List<CookieChange>();

        foreach (var (key, cookie) in afterMap)
        {
            if (!beforeMap.TryGetValue(key, out var beforeCookie))
            {
                added.Add(cookie);
            }
            else if (cookie.Value != beforeCookie.Value)
            {
                changed.Add(new CookieChange
                {
                    Name = cookie.Name,
                    Domain = cookie.Domain,
                    OldValue = beforeCookie.Value,
                    NewValue = cookie.Value,
                });
            }
        }

        foreach (var (key, cookie) in beforeMap)
        {
            if (!afterMap.ContainsKey(key))
            {
                removed.Add(cookie);
            }
        }

        return new CookieDiff
        {
            BeforeSnapshotId = before.Id,
            AfterSnapshotId = after.Id,
            Added = added,
            Removed = removed,
            Changed = changed,
        };
    }

    private static string CookieKey(CapturedCookie c) => $"{c.Name}|{c.Domain}|{c.Path}";
}
