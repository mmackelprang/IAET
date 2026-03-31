using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

public sealed class WebSocketAnalyzer : IStreamAnalyzer
{
    private static readonly HashSet<string> HeartbeatTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ping", "pong", "heartbeat", "ka",
    };

    public bool CanAnalyze(StreamProtocol protocol) => protocol == StreamProtocol.WebSocket;

    public StreamAnalysis Analyze(CapturedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var messageTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasHeartbeat = false;

        if (stream.Frames is not null)
        {
            foreach (var frame in stream.Frames)
            {
                var msgType = ClassifyFrame(frame);
                messageTypes.Add(msgType);

                if (HeartbeatTypes.Contains(msgType))
                    hasHeartbeat = true;
            }
        }

        var subProtocol = stream.Metadata.Properties.TryGetValue("subprotocol", out var sp) ? sp : null;
        var confidence = subProtocol is not null ? ConfidenceLevel.High : ConfidenceLevel.Medium;

        return new StreamAnalysis
        {
            StreamId = stream.Id,
            Protocol = StreamProtocol.WebSocket,
            MessageTypes = messageTypes.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            SubProtocol = subProtocol,
            HasHeartbeat = hasHeartbeat,
            Confidence = confidence,
        };
    }

    private static string ClassifyFrame(StreamFrame frame)
    {
        if (frame.TextPayload is null)
            return "binary";

        try
        {
            using var doc = JsonDocument.Parse(frame.TextPayload);
            if (doc.RootElement.TryGetProperty("type", out var typeProp))
                return typeProp.GetString() ?? "json";

            if (doc.RootElement.TryGetProperty("event", out var eventProp))
                return eventProp.GetString() ?? "json";

            return "json";
        }
        catch (JsonException)
        {
            return "text";
        }
    }
}
