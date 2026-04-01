using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

/// <summary>
/// Analyzes captured WebSocket streams containing SIP messages.
/// Produces a call state machine and timeline from the SIP signaling.
/// </summary>
public sealed class SipAnalyzer : IStreamAnalyzer
{
    public bool CanAnalyze(StreamProtocol protocol) => protocol == StreamProtocol.WebSocket;

    public StreamAnalysis Analyze(CapturedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Only analyze WebSocket streams with SIP subprotocol or SIP-like content
        if (stream.Metadata.Properties.TryGetValue("subprotocol", out var sub) &&
            !string.Equals(sub, "sip", StringComparison.OrdinalIgnoreCase))
            return DefaultAnalysis(stream);

        // Parse all frames as SIP messages
        var messages = new List<SipMessage>();
        if (stream.Frames is not null)
        {
            foreach (var frame in stream.Frames)
            {
                if (frame.TextPayload is not null)
                {
                    var msg = SipMessageParser.TryParse(frame.TextPayload);
                    if (msg is not null)
                        messages.Add(msg);
                }
            }
        }

        if (messages.Count == 0)
            return DefaultAnalysis(stream);

        // Build timeline
        var timeline = SipMessageParser.BuildTimeline(messages);

        // Extract message types for the analysis
        var messageTypes = timeline.Select(t => t.Label).Distinct().OrderBy(l => l, StringComparer.Ordinal).ToList();

        // Build state machine from the message sequence
        var states = new List<string>();
        var transitions = new List<StateTransition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Map SIP messages to call states
        string? prevState = null;
        foreach (var entry in timeline)
        {
            var state = MapToCallState(entry);
            if (!states.Contains(state))
                states.Add(state);

            if (prevState is not null && prevState != state)
            {
                var key = $"{prevState}->{state}";
                if (seen.Add(key))
                {
                    transitions.Add(new StateTransition
                    {
                        From = prevState,
                        To = state,
                        Trigger = entry.Label,
                    });
                }
            }
            prevState = state;
        }

        var stateMachine = new StateMachineModel
        {
            Name = "SIP Call",
            States = states,
            Transitions = transitions,
            InitialState = states.Count > 0 ? states[0] : "",
        };

        // Detect if there's SDP (media negotiation)
        var hasSdp = timeline.Any(t => t.HasSdp);

        // Count distinct Call-IDs (number of calls in this stream)
        var callIds = messages.Where(m => m.CallId is not null).Select(m => m.CallId!).Distinct().Count();

        var limitations = new List<string>();
        if (callIds > 1)
            limitations.Add($"{callIds} distinct calls in one stream — state machine may interleave");

        return new StreamAnalysis
        {
            StreamId = stream.Id,
            Protocol = StreamProtocol.WebSocket,
            MessageTypes = messageTypes,
            SubProtocol = "sip",
            HasHeartbeat = false,
            Confidence = messages.Count >= 5 ? ConfidenceLevel.High : ConfidenceLevel.Medium,
            Limitations = limitations,
            StateMachine = stateMachine,
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "SIP call state names are lowercase identifiers, not locale-sensitive strings")]
    private static string MapToCallState(SipTimelineEntry entry)
    {
        if (entry.IsRequest)
        {
            return entry.Method switch
            {
                "REGISTER" => "registering",
                "INVITE" => "inviting",
                "PRACK" => "provisional_ack",
                "ACK" => "connected",
                "BYE" => "terminating",
                "CANCEL" => "cancelling",
                "UPDATE" => "updating",
                _ => entry.Method!.ToLowerInvariant(),
            };
        }

        return entry.StatusCode switch
        {
            100 => "trying",
            180 => "ringing",
            183 => "early_media",
            200 when entry.CallId is not null => "confirmed",
            200 => "ok",
            401 or 407 => "auth_challenge",
            403 => "forbidden",
            404 => "not_found",
            408 => "timeout",
            480 => "unavailable",
            486 => "busy",
            487 => "cancelled",
            _ => $"status_{entry.StatusCode}",
        };
    }

    private static StreamAnalysis DefaultAnalysis(CapturedStream stream) => new()
    {
        StreamId = stream.Id,
        Protocol = stream.Protocol,
        Confidence = ConfidenceLevel.Low,
    };
}
