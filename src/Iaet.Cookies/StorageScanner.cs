using System.Text.Json;
using Iaet.Core.Abstractions;

namespace Iaet.Cookies;

public sealed class StorageScanner(ICdpSession cdpSession)
{
    private static readonly string[] TokenPatterns = ["TOKEN", "AUTH", "BEARER", "SESSION", "JWT", "API_KEY", "APIKEY", "CREDENTIAL"];

    public async Task<IReadOnlyDictionary<string, string>> ScanLocalStorageAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await cdpSession.SendCommandAsync(
                "Runtime.evaluate",
                new { expression = "JSON.stringify(localStorage)" },
                ct).ConfigureAwait(false);

            var json = result.GetProperty("result").GetProperty("value").GetString();
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
#pragma warning disable CA1031 // CDP errors manifest as various exception types; return empty on any failure
        catch (Exception)
#pragma warning restore CA1031
        {
            return new Dictionary<string, string>();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> ScanSessionStorageAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await cdpSession.SendCommandAsync(
                "Runtime.evaluate",
                new { expression = "JSON.stringify(sessionStorage)" },
                ct).ConfigureAwait(false);

            var json = result.GetProperty("result").GetProperty("value").GetString();
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
#pragma warning disable CA1031 // CDP errors manifest as various exception types; return empty on any failure
        catch (Exception)
#pragma warning restore CA1031
        {
            return new Dictionary<string, string>();
        }
    }

    public static IReadOnlyDictionary<string, string> DetectTokens(IReadOnlyDictionary<string, string> storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in storage)
        {
            var upperKey = key.ToUpperInvariant();
            var isTokenKey = TokenPatterns.Any(p => upperKey.Contains(p, StringComparison.Ordinal));
            var isJwt = value.StartsWith("eyJ", StringComparison.Ordinal);
            var isBearer = value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

            if (isTokenKey || isJwt || isBearer)
            {
                tokens[key] = value;
            }
        }
        return tokens;
    }
}
