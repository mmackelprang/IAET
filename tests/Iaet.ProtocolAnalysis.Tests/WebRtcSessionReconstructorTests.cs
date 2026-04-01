using FluentAssertions;
using Iaet.Core.Models;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class WebRtcSessionReconstructorTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static CapturedStream MakeStream(IReadOnlyList<StreamFrame> frames) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Protocol = StreamProtocol.WebRtc,
        Url = "webrtc://internal",
        StartedAt = DateTimeOffset.UtcNow,
        Metadata = new StreamMetadata([]),
        Frames = frames,
    };

    private static StreamFrame JsonFrame(string json) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Direction = StreamFrameDirection.Sent,
        TextPayload = json,
        SizeBytes = json.Length,
    };

    private static StreamFrame SetLocalOfferFrame(string sdp = MinimalSdp) =>
        JsonFrame($$"""{"action":"setLocalDesc","sdpType":"offer","sdp":"{{Escape(sdp)}}"}""");

    private static StreamFrame SetLocalAnswerFrame(string sdp = MinimalSdp) =>
        JsonFrame($$"""{"action":"setLocalDesc","sdpType":"answer","sdp":"{{Escape(sdp)}}"}""");

    private static StreamFrame SetRemoteOfferFrame(string sdp = MinimalSdp) =>
        JsonFrame($$"""{"action":"setRemoteDesc","sdpType":"offer","sdp":"{{Escape(sdp)}}"}""");

    private static StreamFrame SetRemoteAnswerFrame(string sdp = MinimalSdp) =>
        JsonFrame($$"""{"action":"setRemoteDesc","sdpType":"answer","sdp":"{{Escape(sdp)}}"}""");

    private static StreamFrame LocalIceCandidateFrame() =>
        JsonFrame("""{"action":"localIceCandidate","candidate":"candidate:1 1 UDP 2130706431 192.168.1.1 50000 typ host"}""");

    private static StreamFrame AddIceCandidateFrame() =>
        JsonFrame("""{"action":"addIceCandidate","candidate":"candidate:2 1 UDP 1694498815 1.2.3.4 50001 typ srflx"}""");

    private static StreamFrame StateChangeFrame(string state) =>
        JsonFrame($$"""{"action":"stateChange","state":"{{state}}"}""");

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal)
         .Replace("\r", "\\r", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal);

    private const string MinimalSdp =
        "v=0\r\n" +
        "o=- 1 1 IN IP4 0.0.0.0\r\n" +
        "s=-\r\n" +
        "t=0 0\r\n" +
        "m=audio 9 UDP/TLS/RTP/SAVPF 111\r\n" +
        "a=rtpmap:111 opus/48000/2\r\n";

    private const string SdpWithCodecs =
        "v=0\r\n" +
        "o=- 1 1 IN IP4 0.0.0.0\r\n" +
        "s=-\r\n" +
        "t=0 0\r\n" +
        "m=audio 9 UDP/TLS/RTP/SAVPF 111 103\r\n" +
        "a=rtpmap:111 opus/48000/2\r\n" +
        "a=rtpmap:103 ISAC/16000\r\n" +
        "m=video 9 UDP/TLS/RTP/SAVPF 96\r\n" +
        "a=rtpmap:96 VP8/90000\r\n";

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_reconstructs_outgoing_call_offer_ice_answer_connecting_connected()
    {
        var stream = MakeStream(
        [
            SetLocalOfferFrame(),
            LocalIceCandidateFrame(),
            LocalIceCandidateFrame(),
            SetRemoteAnswerFrame(),
            StateChangeFrame("connecting"),
            StateChangeFrame("connected"),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.StateMachine.Should().NotBeNull();
        result.StateMachine!.Name.Should().Be("WebRTC Session");
        result.StateMachine.InitialState.Should().Be("new");
        result.StateMachine.States.Should().ContainInOrder(["new", "have_local_offer", "stable", "connecting", "connected"]);
        result.StateMachine.Transitions.Should().HaveCount(4);
    }

    [Fact]
    public void Analyze_reconstructs_incoming_call_remote_offer_local_answer_connected()
    {
        var stream = MakeStream(
        [
            SetRemoteOfferFrame(),
            AddIceCandidateFrame(),
            SetLocalAnswerFrame(),
            StateChangeFrame("connected"),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.StateMachine!.States.Should().ContainInOrder(["new", "have_remote_offer", "stable", "connected"]);

        var transitions = result.StateMachine.Transitions;
        transitions.Should().Contain(t => t.From == "new" && t.To == "have_remote_offer" && t.Trigger == "remoteOffer");
        transitions.Should().Contain(t => t.From == "have_remote_offer" && t.To == "stable" && t.Trigger == "createAnswer");
        transitions.Should().Contain(t => t.From == "stable" && t.To == "connected" && t.Trigger == "stateChange");
    }

    [Fact]
    public void Analyze_extracts_codecs_from_sdp_answer()
    {
        var stream = MakeStream(
        [
            SetLocalOfferFrame(),
            SetRemoteAnswerFrame(SdpWithCodecs),
            StateChangeFrame("connected"),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.MessageTypes.Should().Contain(m => m.Contains("opus/48000/2", StringComparison.Ordinal));
        result.MessageTypes.Should().Contain(m => m.Contains("ISAC/16000", StringComparison.Ordinal));
        result.MessageTypes.Should().Contain(m => m.Contains("VP8/90000", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_extracts_codecs_from_sdp_offer_when_no_answer()
    {
        var stream = MakeStream(
        [
            SetLocalOfferFrame(SdpWithCodecs),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.MessageTypes.Should().Contain(m => m.Contains("opus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_returns_low_confidence_for_empty_frames()
    {
        var stream = MakeStream([]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.Confidence.Should().Be(ConfidenceLevel.Low);
        result.StateMachine.Should().BeNull();
    }

    [Fact]
    public void Analyze_returns_medium_confidence_without_sdp()
    {
        var stream = MakeStream(
        [
            StateChangeFrame("connecting"),
            StateChangeFrame("connected"),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.Confidence.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public void Analyze_returns_high_confidence_when_sdp_present()
    {
        var stream = MakeStream(
        [
            SetLocalOfferFrame(),
            StateChangeFrame("connected"),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_counts_ice_candidates()
    {
        var stream = MakeStream(
        [
            SetLocalOfferFrame(),
            LocalIceCandidateFrame(),
            LocalIceCandidateFrame(),
            LocalIceCandidateFrame(),
            AddIceCandidateFrame(),
            AddIceCandidateFrame(),
            SetRemoteAnswerFrame(),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.MessageTypes.Should().Contain("5 ICE candidates");
    }

    [Fact]
    public void Analyze_lists_sdp_offer_and_answer_in_message_types()
    {
        var stream = MakeStream(
        [
            SetLocalOfferFrame(),
            SetRemoteAnswerFrame(),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.MessageTypes.Should().Contain("SDP offer");
        result.MessageTypes.Should().Contain("SDP answer");
    }

    [Fact]
    public void Analyze_tolerates_non_json_frames()
    {
        var stream = MakeStream(
        [
            JsonFrame("not json at all"),
            SetLocalOfferFrame(),
            JsonFrame("{broken"),
            StateChangeFrame("connected"),
        ]);

        var act = () => new WebRtcSessionReconstructor().Analyze(stream);

        act.Should().NotThrow();
        var result = act();
        result.Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_handles_closed_state_transition()
    {
        var stream = MakeStream(
        [
            SetLocalOfferFrame(),
            SetRemoteAnswerFrame(),
            StateChangeFrame("connecting"),
            StateChangeFrame("connected"),
            StateChangeFrame("closed"),
        ]);

        var result = new WebRtcSessionReconstructor().Analyze(stream);

        result.StateMachine!.States.Should().Contain("closed");
        result.StateMachine.Transitions.Should().Contain(t => t.From == "connected" && t.To == "closed");
    }

    [Fact]
    public void CanAnalyze_returns_true_only_for_webrtc()
    {
        var reconstructor = new WebRtcSessionReconstructor();
        reconstructor.CanAnalyze(StreamProtocol.WebRtc).Should().BeTrue();
        reconstructor.CanAnalyze(StreamProtocol.WebSocket).Should().BeFalse();
        reconstructor.CanAnalyze(StreamProtocol.ServerSentEvents).Should().BeFalse();
    }
}
