namespace Iaet.ProtocolAnalysis;

/// <summary>
/// Parses SIP (Session Initiation Protocol) messages from text.
/// Protocol-agnostic — works with any SIP-over-WebSocket, SIP-over-TCP, or SIP-over-UDP capture.
/// </summary>
public static class SipMessageParser
{
    public static SipMessage? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = text.Split('\n');
        if (lines.Length == 0)
            return null;

        var firstLine = lines[0].Trim('\r');

        // Determine if this is a request or response
        string? method = null;
        string? requestUri = null;
        int? statusCode = null;
        string? reasonPhrase = null;

        if (firstLine.StartsWith("SIP/", StringComparison.OrdinalIgnoreCase))
        {
            // Response: SIP/2.0 200 OK
            var parts = firstLine.Split(' ', 3);
            if (parts.Length >= 2 && int.TryParse(parts[1], out var code))
            {
                statusCode = code;
                reasonPhrase = parts.Length >= 3 ? parts[2] : null;
            }
        }
        else
        {
            // Request: INVITE sip:user@host SIP/2.0
            var parts = firstLine.Split(' ', 3);
            if (parts.Length >= 3 && parts[2].StartsWith("SIP/", StringComparison.OrdinalIgnoreCase))
            {
                method = parts[0];
                requestUri = parts[1];
            }
        }

        if (method is null && statusCode is null)
            return null;

        // Parse headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodyStartIndex = -1;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            if (string.IsNullOrEmpty(line))
            {
                bodyStartIndex = i + 1;
                break;
            }

            var colonIdx = line.IndexOf(':', StringComparison.Ordinal);
            if (colonIdx > 0)
            {
                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..].Trim();
                headers[key] = value;
            }
        }

        // Extract body (typically SDP)
        string? body = null;
        if (bodyStartIndex > 0 && bodyStartIndex < lines.Length)
        {
            body = string.Join("\n", lines[bodyStartIndex..]).TrimEnd();
            if (string.IsNullOrWhiteSpace(body))
                body = null;
        }

        return new SipMessage
        {
            IsRequest = method is not null,
            Method = method,
            RequestUri = requestUri,
            StatusCode = statusCode,
            ReasonPhrase = reasonPhrase,
            Headers = headers,
            Body = body,
            CallId = headers.TryGetValue("Call-ID", out var callId) ? callId : null,
            CSeq = headers.TryGetValue("CSeq", out var cseq) ? cseq : null,
            From = headers.TryGetValue("From", out var from) ? from : null,
            To = headers.TryGetValue("To", out var to) ? to : null,
            ContentType = headers.TryGetValue("Content-Type", out var ct) ? ct : null,
        };
    }

    /// <summary>
    /// Extract a structured call timeline from a sequence of SIP messages.
    /// </summary>
    public static IReadOnlyList<SipTimelineEntry> BuildTimeline(IReadOnlyList<SipMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var entries = new List<SipTimelineEntry>();

        foreach (var msg in messages)
        {
            var label = msg.IsRequest
                ? msg.Method ?? "UNKNOWN"
                : $"{msg.StatusCode} {msg.ReasonPhrase}";

            entries.Add(new SipTimelineEntry
            {
                Label = label,
                IsRequest = msg.IsRequest,
                Method = msg.Method,
                StatusCode = msg.StatusCode,
                CallId = msg.CallId,
                HasSdp = msg.ContentType?.Contains("sdp", StringComparison.OrdinalIgnoreCase) == true
                         || msg.Body?.Contains("v=0", StringComparison.Ordinal) == true,
            });
        }

        return entries;
    }
}

public sealed record SipMessage
{
    public bool IsRequest { get; init; }
    public string? Method { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public string? RequestUri { get; init; }
    public int? StatusCode { get; init; }
    public string? ReasonPhrase { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public string? Body { get; init; }
    public string? CallId { get; init; }
    public string? CSeq { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? ContentType { get; init; }
}

public sealed record SipTimelineEntry
{
    public required string Label { get; init; }
    public required bool IsRequest { get; init; }
    public string? Method { get; init; }
    public int? StatusCode { get; init; }
    public string? CallId { get; init; }
    public bool HasSdp { get; init; }
}
