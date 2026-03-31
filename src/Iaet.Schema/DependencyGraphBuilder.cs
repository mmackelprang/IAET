using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.Schema;

public static class DependencyGraphBuilder
{
    private const int MinTokenLength = 6;

    public static IReadOnlyList<RequestDependency> Build(IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var responseTokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var dependencies = new List<RequestDependency>();

        foreach (var req in requests)
        {
            var signature = GetPathSignature(req);
            if (signature is null)
                continue;

            if (req.ResponseBody is not null)
            {
                foreach (var (key, value) in ExtractJsonValues(req.ResponseBody))
                {
                    if (value.Length >= MinTokenLength)
                        responseTokens[value] = signature;
                }
            }
        }

        foreach (var req in requests)
        {
            var signature = GetPathSignature(req);
            if (signature is null)
                continue;

            foreach (var (headerKey, headerValue) in req.RequestHeaders)
            {
                if (headerValue == "<REDACTED>")
                    continue;

                if (responseTokens.TryGetValue(headerValue, out var source) && source != signature)
                {
                    dependencies.Add(new RequestDependency
                    {
                        From = source,
                        To = signature,
                        Reason = $"{headerKey} header contains value from response",
                        SharedField = headerKey,
                    });
                }
            }

            if (req.Url.Contains('?', StringComparison.Ordinal) &&
                Uri.TryCreate(req.Url, UriKind.Absolute, out var uriForQuery))
            {
                var query = uriForQuery.Query;
                foreach (var (tokenValue, source) in responseTokens)
                {
                    if (source != signature && query.Contains(tokenValue, StringComparison.Ordinal))
                    {
                        dependencies.Add(new RequestDependency
                        {
                            From = source,
                            To = signature,
                            Reason = "Query parameter contains value from response",
                        });
                    }
                }
            }
        }

        return dependencies;
    }

    private static string? GetPathSignature(CapturedRequest req)
    {
        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
            return null;
        return $"{req.HttpMethod} {uri.AbsolutePath}";
    }

    private static IEnumerable<KeyValuePair<string, string>> ExtractJsonValues(string body)
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
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (val is not null)
                        yield return new KeyValuePair<string, string>(prop.Name, val);
                }
            }
        }
    }
}
