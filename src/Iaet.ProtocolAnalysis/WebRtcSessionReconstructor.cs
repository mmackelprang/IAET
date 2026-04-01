using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

/// <summary>
/// Analyzes captured WebRTC streams and reconstructs the session lifecycle:
/// create → setLocalDescription → setRemoteDescription → ICE gathering → connecting → connected → closed.
/// </summary>
public sealed class WebRtcSessionReconstructor : IStreamAnalyzer
{
    public bool CanAnalyze(StreamProtocol protocol) => protocol == StreamProtocol.WebRtc;

    public StreamAnalysis Analyze(CapturedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Frames is null || stream.Frames.Count == 0)
            return new StreamAnalysis { StreamId = stream.Id, Protocol = StreamProtocol.WebRtc, Confidence = ConfidenceLevel.Low };

        var sdpOffer = (string?)null;
        var sdpAnswer = (string?)null;
        var iceCandidateCount = 0;
        var states = new List<string> { "new" };
        var transitions = new List<StateTransition>();
        var prevState = "new";

        foreach (var frame in stream.Frames)
        {
            if (frame.TextPayload is null) continue;

            try
            {
                using var doc = JsonDocument.Parse(frame.TextPayload);
                var root = doc.RootElement;
                var action = root.GetProperty("action").GetString() ?? "";

                switch (action)
                {
                    case "setLocalDesc":
                        var localType = root.TryGetProperty("sdpType", out var lt) ? lt.GetString() : null;
                        if (localType == "offer")
                        {
                            sdpOffer = root.TryGetProperty("sdp", out var so) ? so.GetString() : null;
                            AddTransition(ref prevState, "have_local_offer", "createOffer", states, transitions);
                        }
                        else if (localType == "answer")
                        {
                            sdpAnswer = root.TryGetProperty("sdp", out var sa) ? sa.GetString() : null;
                            AddTransition(ref prevState, "stable", "createAnswer", states, transitions);
                        }
                        break;

                    case "setRemoteDesc":
                        var remoteType = root.TryGetProperty("sdpType", out var rt) ? rt.GetString() : null;
                        if (remoteType == "offer")
                        {
                            sdpOffer = root.TryGetProperty("sdp", out var so) ? so.GetString() : null;
                            AddTransition(ref prevState, "have_remote_offer", "remoteOffer", states, transitions);
                        }
                        else if (remoteType == "answer")
                        {
                            sdpAnswer = root.TryGetProperty("sdp", out var sa) ? sa.GetString() : null;
                            AddTransition(ref prevState, "stable", "remoteAnswer", states, transitions);
                        }
                        break;

                    case "localIceCandidate":
                    case "addIceCandidate":
                        iceCandidateCount++;
                        break;

                    case "stateChange":
                        var newState = root.TryGetProperty("state", out var st) ? st.GetString() : null;
                        if (newState is not null)
                            AddTransition(ref prevState, newState, "stateChange", states, transitions);
                        break;
                }
            }
            catch (JsonException) { }
        }

        // Parse SDP for codec info
        var codecs = new List<string>();
        var sdpToParse = sdpAnswer ?? sdpOffer;
        if (sdpToParse is not null)
        {
            var sdpResult = SdpParser.Parse(sdpToParse);
            foreach (var media in sdpResult.MediaSections)
                codecs.AddRange(media.Codecs);
        }

        var messageTypes = new List<string>();
        if (sdpOffer is not null) messageTypes.Add("SDP offer");
        if (sdpAnswer is not null) messageTypes.Add("SDP answer");
        if (iceCandidateCount > 0) messageTypes.Add($"{iceCandidateCount} ICE candidates");
        if (codecs.Count > 0) messageTypes.Add($"Codecs: {string.Join(", ", codecs.Distinct())}");

        var stateMachine = new StateMachineModel
        {
            Name = "WebRTC Session",
            States = states,
            Transitions = transitions,
            InitialState = "new",
        };

        return new StreamAnalysis
        {
            StreamId = stream.Id,
            Protocol = StreamProtocol.WebRtc,
            MessageTypes = messageTypes,
            SubProtocol = null,
            HasHeartbeat = false,
            Confidence = (sdpOffer is not null || sdpAnswer is not null) ? ConfidenceLevel.High : ConfidenceLevel.Medium,
            StateMachine = stateMachine,
        };
    }

    private static void AddTransition(ref string prevState, string newState, string trigger,
        List<string> states, List<StateTransition> transitions)
    {
        if (!states.Contains(newState))
            states.Add(newState);

        if (prevState != newState)
        {
            transitions.Add(new StateTransition { From = prevState, To = newState, Trigger = trigger });
            prevState = newState;
        }
    }
}
