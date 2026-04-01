namespace Iaet.JsAnalysis;

using System.Text.Json;
using Iaet.Core.Models;

/// <summary>
/// A single value correlation between an endpoint response and a consumption site.
/// </summary>
public sealed record ValueCorrelation
{
    public required string Value { get; init; }
    public required string SourceEndpoint { get; init; }
    public required int SourcePosition { get; init; }
    public required string ConsumedBy { get; init; }
    public required string ConsumedContext { get; init; }
    public required string SuggestedName { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
}

/// <summary>
/// Correlates values across endpoints to resolve protojson field names.
/// When a value appears in one endpoint's response and another's request
/// (or captured stream data), the field's meaning is inferred from the consumption context.
/// </summary>
public static class CrossEndpointCorrelator
{
    /// <summary>Maximum length of a value snippet included in correlation results.</summary>
    private const int MaxDisplayLength = 40;

    /// <summary>Minimum string/number length to be considered meaningful for correlation.</summary>
    private const int MinValueLength = 6;

    /// <summary>Maximum value length to track — longer values are likely body blobs.</summary>
    private const int MaxValueLength = 500;

    /// <summary>
    /// Correlate values across all captured requests in a session.
    /// </summary>
    public static IReadOnlyList<ValueCorrelation> Correlate(IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count < 2)
            return [];

        // Phase 1: Extract values from all response bodies with their source position
        var responseValues = new List<(string Value, string Endpoint, int Position)>();

        foreach (var req in requests)
        {
            if (req.ResponseBody is null) continue;
            var endpoint = GetEndpointSignature(req);

            try
            {
                using var doc = JsonDocument.Parse(req.ResponseBody);
                ExtractValues(doc.RootElement, endpoint, 0, responseValues);
            }
            catch (JsonException) { /* Non-JSON response body — skip */ }
        }

        // Phase 2: Build a lookup of significant values (skip very short or very common ones)
        var valueLookup = BuildValueLookup(responseValues);

        // Phase 3: Check if any response value appears in request contexts
        var correlations = new List<ValueCorrelation>();

        foreach (var req in requests)
        {
            var endpoint = GetEndpointSignature(req);
            MatchRequestHeaders(req, endpoint, valueLookup, correlations);
            MatchUrlQueryParams(req, endpoint, valueLookup, correlations);
            MatchRequestBody(req, endpoint, valueLookup, correlations);
        }

        return Deduplicate(correlations);
    }

    /// <summary>
    /// Correlate values with captured stream data (SIP messages, WebSocket frames, etc.).
    /// </summary>
    public static IReadOnlyList<ValueCorrelation> CorrelateWithStreams(
        IReadOnlyList<CapturedRequest> requests,
        IReadOnlyList<CapturedStream> streams)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(streams);

        // Extract values from responses
        var responseValues = new List<(string Value, string Endpoint, int Position)>();
        foreach (var req in requests)
        {
            if (req.ResponseBody is null) continue;
            var endpoint = GetEndpointSignature(req);
            try
            {
                using var doc = JsonDocument.Parse(req.ResponseBody);
                ExtractValues(doc.RootElement, endpoint, 0, responseValues);
            }
            catch (JsonException) { /* Non-JSON response body — skip */ }
        }

        var valueLookup = BuildValueLookup(responseValues);
        var correlations = new List<ValueCorrelation>();

        // Check stream frames for response values
        foreach (var stream in streams)
        {
            if (stream.Frames is null) continue;
            foreach (var frame in stream.Frames)
            {
                if (frame.TextPayload is null) continue;
                foreach (var kvp in valueLookup)
                {
                    if (!frame.TextPayload.Contains(kvp.Key, StringComparison.Ordinal))
                        continue;

                    foreach (var (srcEndpoint, srcPosition) in kvp.Value)
                    {
                        var context = InferStreamContext(stream.Protocol, frame.TextPayload, kvp.Key);
                        correlations.Add(new ValueCorrelation
                        {
                            Value = Truncate(kvp.Key),
                            SourceEndpoint = srcEndpoint,
                            SourcePosition = srcPosition,
                            ConsumedBy = $"stream:{stream.Protocol}",
                            ConsumedContext = context,
                            SuggestedName = InferNameFromStreamContext(context),
                            Confidence = ConfidenceLevel.High,
                        });
                    }
                }
            }
        }

        return Deduplicate(correlations);
    }

    private static Dictionary<string, List<(string Endpoint, int Position)>> BuildValueLookup(
        List<(string Value, string Endpoint, int Position)> responseValues)
    {
        var valueLookup = new Dictionary<string, List<(string Endpoint, int Position)>>(StringComparer.Ordinal);
        foreach (var (value, endpoint, position) in responseValues)
        {
            if (value.Length < MinValueLength || value.Length > MaxValueLength)
                continue;

            if (!valueLookup.TryGetValue(value, out var list))
            {
                list = [];
                valueLookup[value] = list;
            }

            list.Add((endpoint, position));
        }

        return valueLookup;
    }

    private static void MatchRequestHeaders(
        CapturedRequest req,
        string endpoint,
        Dictionary<string, List<(string Endpoint, int Position)>> valueLookup,
        List<ValueCorrelation> correlations)
    {
        foreach (var header in req.RequestHeaders)
        {
            if (string.Equals(header.Value, "<REDACTED>", StringComparison.Ordinal))
                continue;

            if (!valueLookup.TryGetValue(header.Value, out var sources))
                continue;

            foreach (var (srcEndpoint, srcPosition) in sources)
            {
                if (string.Equals(srcEndpoint, endpoint, StringComparison.Ordinal))
                    continue; // Skip self-correlation

                correlations.Add(new ValueCorrelation
                {
                    Value = Truncate(header.Value),
                    SourceEndpoint = srcEndpoint,
                    SourcePosition = srcPosition,
                    ConsumedBy = endpoint,
                    ConsumedContext = $"request_header:{header.Key}",
                    SuggestedName = InferNameFromHeader(header.Key),
                    Confidence = ConfidenceLevel.High,
                });
            }
        }
    }

    private static void MatchUrlQueryParams(
        CapturedRequest req,
        string endpoint,
        Dictionary<string, List<(string Endpoint, int Position)>> valueLookup,
        List<ValueCorrelation> correlations)
    {
        if (!req.Url.Contains('?', StringComparison.Ordinal))
            return;

        if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
            return;

        var query = uri.Query;
        foreach (var kvp in valueLookup)
        {
            if (!query.Contains(kvp.Key, StringComparison.Ordinal))
                continue;

            foreach (var (srcEndpoint, srcPosition) in kvp.Value)
            {
                if (string.Equals(srcEndpoint, endpoint, StringComparison.Ordinal))
                    continue;

                correlations.Add(new ValueCorrelation
                {
                    Value = Truncate(kvp.Key),
                    SourceEndpoint = srcEndpoint,
                    SourcePosition = srcPosition,
                    ConsumedBy = endpoint,
                    ConsumedContext = "request_url_query",
                    SuggestedName = "queryParam",
                    Confidence = ConfidenceLevel.Medium,
                });
            }
        }
    }

    private static void MatchRequestBody(
        CapturedRequest req,
        string endpoint,
        Dictionary<string, List<(string Endpoint, int Position)>> valueLookup,
        List<ValueCorrelation> correlations)
    {
        if (req.RequestBody is null)
            return;

        foreach (var kvp in valueLookup)
        {
            if (!req.RequestBody.Contains(kvp.Key, StringComparison.Ordinal))
                continue;

            foreach (var (srcEndpoint, srcPosition) in kvp.Value)
            {
                if (string.Equals(srcEndpoint, endpoint, StringComparison.Ordinal))
                    continue;

                correlations.Add(new ValueCorrelation
                {
                    Value = Truncate(kvp.Key),
                    SourceEndpoint = srcEndpoint,
                    SourcePosition = srcPosition,
                    ConsumedBy = endpoint,
                    ConsumedContext = "request_body",
                    SuggestedName = $"sharedWith_{SanitizeName(endpoint)}",
                    Confidence = ConfidenceLevel.Medium,
                });
            }
        }
    }

    private static void ExtractValues(JsonElement element, string endpoint, int position,
        List<(string Value, string Endpoint, int Position)> results)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var str = element.GetString();
                if (str is not null)
                    results.Add((str, endpoint, position));
                break;
            case JsonValueKind.Number:
                results.Add((element.GetRawText(), endpoint, position));
                break;
            case JsonValueKind.Array:
                var idx = 0;
                foreach (var child in element.EnumerateArray())
                {
                    ExtractValues(child, endpoint, idx, results);
                    idx++;
                }

                break;
            default:
                // Other value kinds (Object, True, False, Null, Undefined) are not tracked
                break;
        }
    }

    private static string GetEndpointSignature(CapturedRequest req)
    {
        if (Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
            return $"{req.HttpMethod} {uri.AbsolutePath}";
        return $"{req.HttpMethod} {req.Url}";
    }

    private static string InferNameFromHeader(string headerKey)
    {
        return headerKey.ToUpperInvariant() switch
        {
            "AUTHORIZATION" => "authToken",
            "X-GOOG-API-KEY" or "X-API-KEY" => "apiKey",
            "X-SESSION-ID" => "sessionId",
            "X-CSRF-TOKEN" or "X-XSRF-TOKEN" => "csrfToken",
            "COOKIE" => "sessionCookie",
            _ => $"headerValue_{headerKey.Replace("-", "", StringComparison.Ordinal)}",
        };
    }

    private static string InferStreamContext(StreamProtocol protocol, string frameText, string value)
    {
        if (protocol == StreamProtocol.WebSocket)
        {
            // Check for SIP REGISTER context
            if (frameText.Contains("REGISTER", StringComparison.Ordinal) &&
                frameText.Contains("SIP/2.0", StringComparison.Ordinal))
            {
                if (frameText.Contains($"sip:{value}", StringComparison.OrdinalIgnoreCase))
                    return "sip_register_domain";
                if (frameText.Contains("From:", StringComparison.OrdinalIgnoreCase))
                    return "sip_from";
                return "sip_register";
            }

            // Check for SIP INVITE context
            if (frameText.Contains("INVITE", StringComparison.Ordinal))
            {
                if (frameText.Contains($"sip:{value}", StringComparison.OrdinalIgnoreCase))
                    return "sip_invite_target";
                return "sip_invite";
            }
        }

        return $"stream_{protocol}";
    }

    private static string InferNameFromStreamContext(string context)
    {
        return context switch
        {
            "sip_register_domain" => "sipDomain",
            "sip_register" => "sipRegistrationValue",
            "sip_from" => "sipFromAddress",
            "sip_invite_target" => "sipCallTarget",
            "sip_invite" => "sipInviteValue",
            _ => "streamValue",
        };
    }

    private static string SanitizeName(string endpoint)
    {
        var parts = endpoint.Split('/');
        var last = parts.Length > 0 ? parts[^1] : "unknown";
        if (last.Length > 0)
            return string.Concat(char.ToUpperInvariant(last[0]).ToString(), last.AsSpan(1));
        return "Unknown";
    }

    private static string Truncate(string value)
    {
        return value.Length > MaxDisplayLength
            ? string.Concat(value.AsSpan(0, MaxDisplayLength - 3), "...")
            : value;
    }

    private static List<ValueCorrelation> Deduplicate(List<ValueCorrelation> correlations)
    {
        // Deduplicate by source endpoint + position (keep highest confidence)
        return correlations
            .GroupBy(c => $"{c.SourceEndpoint}:{c.SourcePosition}", StringComparer.Ordinal)
            .Select(g => g.OrderBy(c => c.Confidence).First()) // High=0 is best
            .ToList();
    }
}
