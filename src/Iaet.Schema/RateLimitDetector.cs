using System.Globalization;
using Iaet.Core.Models;

namespace Iaet.Schema;

public static class RateLimitDetector
{
    public static IReadOnlyList<RateLimitInfo> Detect(IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var results = new Dictionary<string, RateLimitInfo>(StringComparer.Ordinal);

        foreach (var req in requests)
        {
            if (req.ResponseStatus != 429)
                continue;

            var sig = GetPathSignature(req);
            if (sig is null)
                continue;

            int? retryAfter = null;

            if (req.ResponseHeaders.TryGetValue("Retry-After", out var retryVal) &&
                int.TryParse(retryVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                retryAfter = seconds;
            }

            results.TryAdd(sig, new RateLimitInfo
            {
                Endpoint = sig,
                RetryAfterSeconds = retryAfter,
            });
        }

        return results.Values.ToList();
    }

    private static string? GetPathSignature(CapturedRequest req)
    {
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
            return null;
        return $"{req.HttpMethod} {uri.AbsolutePath}";
    }
}

public sealed record RateLimitInfo
{
    public required string Endpoint { get; init; }
    public int? RetryAfterSeconds { get; init; }
}
