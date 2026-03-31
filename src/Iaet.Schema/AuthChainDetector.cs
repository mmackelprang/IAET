using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.Schema;

public static class AuthChainDetector
{
    private static readonly HashSet<string> AuthResponseFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token", "token", "auth_token", "jwt", "session_token", "id_token", "refresh_token",
    };

    private static readonly HashSet<string> AuthHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "x-auth-token", "x-session-id", "x-csrf-token",
    };

    public static IReadOnlyList<AuthChain> Detect(IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var providers = new List<AuthChainStep>();
        var consumers = new List<AuthChainStep>();

        foreach (var req in requests)
        {
            var sig = GetPathSignature(req);
            if (sig is null)
                continue;

            if (req.ResponseBody is not null)
            {
                foreach (var field in ExtractAuthFields(req.ResponseBody))
                {
                    providers.Add(new AuthChainStep { Endpoint = sig, Provides = field, Type = "token" });
                }
            }

            foreach (var header in req.RequestHeaders.Keys)
            {
                if (AuthHeaders.Contains(header))
                {
                    consumers.Add(new AuthChainStep { Endpoint = sig, Consumes = header, Type = "header" });
                }
            }
        }

        if (providers.Count == 0 && consumers.Count == 0)
            return [];

        var steps = new List<AuthChainStep>();
        steps.AddRange(providers);
        steps.AddRange(consumers);

        return [new AuthChain { Name = "Detected auth chain", Steps = steps }];
    }

    private static string? GetPathSignature(CapturedRequest req)
    {
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
            return null;
        return $"{req.HttpMethod} {uri.AbsolutePath}";
    }

    private static IEnumerable<string> ExtractAuthFields(string body)
    {
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (AuthResponseFields.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.String)
                    yield return prop.Name;
            }
        }
    }
}
