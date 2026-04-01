namespace Iaet.JsAnalysis;

/// <summary>
/// Generates domain-specific field name hints based on the API endpoint path.
/// </summary>
public static class EndpointContextEnricher
{
    private static readonly Dictionary<string, IReadOnlyList<string>> DomainHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["account"] = ["phoneNumber", "email", "displayName", "devices", "settings", "billing", "features", "voicemail", "callingConfig"],
        ["sipregisterinfo"] = ["credentials", "sipServer", "websocketUrl", "realm", "nonce", "transportConfig"],
        ["thread"] = ["threadId", "participants", "messages", "lastMessage", "unreadCount", "timestamp"],
        ["message"] = ["messageId", "threadId", "body", "sender", "recipient", "timestamp", "status"],
        ["sendsms"] = ["recipient", "messageBody", "threadId", "timestamp", "messageId"],
        ["call"] = ["callId", "caller", "callee", "duration", "startTime", "endTime", "status"],
        ["contact"] = ["contactId", "name", "phoneNumber", "email", "avatarUrl"],
        ["voicemail"] = ["voicemailId", "caller", "duration", "transcript", "audioUrl", "timestamp"],
        ["inboundcallrule"] = ["ruleId", "contactGroup", "action", "voicemailGreeting", "ringConfig"],
        ["numbertransfer"] = ["transferId", "phoneNumber", "status", "provider"],
        ["ringgroup"] = ["groupId", "name", "members", "ringPattern"],
        ["search"] = ["query", "results", "totalCount", "nextPageToken"],
    };

    /// <summary>
    /// Get domain-specific field name hints based on the endpoint path.
    /// Returns positional hints — index 0 = hint for field 0, etc.
    /// </summary>
    public static IReadOnlyList<string> GetDomainHints(string endpointPath)
    {
        ArgumentNullException.ThrowIfNull(endpointPath);

        // Extract the resource name from the path
        // e.g., "/voice/v1/voiceclient/sipregisterinfo/get" -> "sipregisterinfo"
        // e.g., "/$rpc/google.internal.communications.instantmessaging.v1.Messaging/SendMessage" -> "message"
        var segments = endpointPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Try each segment as a domain key (reverse order to prioritize resource name over path prefixes)
        foreach (var segment in segments.Reverse())
        {
            // Skip common non-resource segments
            if (segment is "get" or "list" or "update" or "create" or "delete" or "v1" or "v2" or "api" or "voice" or "voiceclient")
                continue;

            // Try exact match
            if (DomainHints.TryGetValue(segment, out var hints))
                return hints;

            // Try partial match (e.g., "SendMessage" matches "message")
            foreach (var (key, value) in DomainHints)
            {
                if (segment.Contains(key, StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }

        return [];
    }
}
