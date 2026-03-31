using System.Text.Json;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Cookies;

public sealed class CookieCollector(ICdpSession cdpSession) : ICookieCollector
{
    public async Task<IReadOnlyList<CapturedCookie>> CollectAllAsync(CancellationToken ct = default)
    {
        var result = await cdpSession.SendCommandAsync("Network.getAllCookies", null, ct).ConfigureAwait(false);
        var cookies = new List<CapturedCookie>();

        if (result.TryGetProperty("cookies", out var cookiesArray))
        {
            foreach (var c in cookiesArray.EnumerateArray())
            {
                var expiresUnix = c.GetProperty("expires").GetDouble();
                cookies.Add(new CapturedCookie
                {
                    Name = c.GetProperty("name").GetString() ?? string.Empty,
                    Value = c.GetProperty("value").GetString() ?? string.Empty,
                    Domain = c.GetProperty("domain").GetString() ?? string.Empty,
                    Path = c.GetProperty("path").GetString() ?? string.Empty,
                    Expires = expiresUnix > 0
                        ? DateTimeOffset.FromUnixTimeSeconds((long)expiresUnix)
                        : null,
                    HttpOnly = c.GetProperty("httpOnly").GetBoolean(),
                    Secure = c.GetProperty("secure").GetBoolean(),
                    SameSite = c.GetProperty("sameSite").GetString(),
                    Size = c.GetProperty("size").GetInt64(),
                });
            }
        }

        return cookies;
    }

    public async Task<CookieSnapshotInfo> TakeSnapshotAsync(string projectName, string source, CancellationToken ct = default)
    {
        var cookies = await CollectAllAsync(ct).ConfigureAwait(false);
        return new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(),
            ProjectName = projectName,
            CapturedAt = DateTimeOffset.UtcNow,
            Source = source,
            Cookies = cookies,
        };
    }
}
