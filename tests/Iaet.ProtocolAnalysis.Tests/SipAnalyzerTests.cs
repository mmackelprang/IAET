using FluentAssertions;
using Iaet.Core.Models;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class SipAnalyzerTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static CapturedStream MakeStream(
        IReadOnlyList<StreamFrame> frames,
        string? subprotocol = null)
    {
        var meta = new Dictionary<string, string>();
        if (subprotocol is not null)
            meta["subprotocol"] = subprotocol;

        return new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Protocol = StreamProtocol.WebSocket,
            Url = "wss://sip.example.com/ws",
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = new StreamMetadata(meta),
            Frames = frames,
        };
    }

    private static StreamFrame SipFrame(string text) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Direction = StreamFrameDirection.Sent,
        TextPayload = text,
        SizeBytes = text.Length,
    };

    private static string Register(string callId = "reg1") =>
        $"REGISTER sip:pbx.example.com SIP/2.0\r\nCall-ID: {callId}\r\nCSeq: 1 REGISTER\r\nFrom: <sip:alice@example.com>\r\nTo: <sip:alice@example.com>\r\n\r\n";

    private static string Ok200(string callId = "reg1") =>
        $"SIP/2.0 200 OK\r\nCall-ID: {callId}\r\nCSeq: 1 REGISTER\r\n\r\n";

    private static string Invite(string callId = "call1") =>
        $"INVITE sip:bob@pbx.example.com SIP/2.0\r\nCall-ID: {callId}\r\nCSeq: 1 INVITE\r\nFrom: <sip:alice@example.com>\r\nTo: <sip:bob@example.com>\r\nContent-Type: application/sdp\r\n\r\nv=0\r\no=- 1 1 IN IP4 0.0.0.0\r\n";

    private static string Trying(string callId = "call1") =>
        $"SIP/2.0 100 Trying\r\nCall-ID: {callId}\r\nCSeq: 1 INVITE\r\n\r\n";

    private static string SessionProgress183(string callId = "call1") =>
        $"SIP/2.0 183 Session Progress\r\nCall-ID: {callId}\r\nCSeq: 1 INVITE\r\nContent-Type: application/sdp\r\n\r\nv=0\r\no=- 2 2 IN IP4 0.0.0.0\r\n";

    private static string Prack(string callId = "call1") =>
        $"PRACK sip:bob@pbx.example.com SIP/2.0\r\nCall-ID: {callId}\r\nCSeq: 2 PRACK\r\nFrom: <sip:alice@example.com>\r\nTo: <sip:bob@example.com>\r\n\r\n";

    private static string Ack(string callId = "call1") =>
        $"ACK sip:bob@pbx.example.com SIP/2.0\r\nCall-ID: {callId}\r\nCSeq: 1 ACK\r\nFrom: <sip:alice@example.com>\r\nTo: <sip:bob@example.com>\r\n\r\n";

    private static string Bye(string callId = "call1") =>
        $"BYE sip:bob@pbx.example.com SIP/2.0\r\nCall-ID: {callId}\r\nCSeq: 3 BYE\r\nFrom: <sip:alice@example.com>\r\nTo: <sip:bob@example.com>\r\n\r\n";

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_produces_state_machine_for_register_invite_prack_ack_bye()
    {
        var stream = MakeStream(
        [
            SipFrame(Register()),
            SipFrame(Ok200()),
            SipFrame(Invite()),
            SipFrame(Trying()),
            SipFrame(SessionProgress183()),
            SipFrame(Prack()),
            SipFrame(Ack()),
            SipFrame(Bye()),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        result.StateMachine.Should().NotBeNull();
        result.StateMachine!.Name.Should().Be("SIP Call");
        result.StateMachine.States.Should().Contain(["registering", "confirmed", "inviting", "trying", "early_media", "provisional_ack", "connected", "terminating"]);
        result.StateMachine.Transitions.Should().NotBeEmpty();
        result.StateMachine.InitialState.Should().Be("registering");
    }

    [Fact]
    public void Analyze_produces_correct_message_types()
    {
        var stream = MakeStream(
        [
            SipFrame(Register()),
            SipFrame(Ok200()),
            SipFrame(Invite()),
            SipFrame(Trying()),
            SipFrame(Ack()),
            SipFrame(Bye()),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        result.MessageTypes.Should().Contain("REGISTER");
        result.MessageTypes.Should().Contain("INVITE");
        result.MessageTypes.Should().Contain("ACK");
        result.MessageTypes.Should().Contain("BYE");
        result.MessageTypes.Should().Contain("100 Trying");
        result.MessageTypes.Should().Contain("200 OK");
        // Message types are deduplicated and sorted
        result.MessageTypes.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void Analyze_detects_sdp_in_183_session_progress()
    {
        var stream = MakeStream(
        [
            SipFrame(Invite()),
            SipFrame(Trying()),
            SipFrame(SessionProgress183()),
            SipFrame(Prack()),
            SipFrame(Ack()),
            SipFrame(Bye()),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        // The early_media state comes from the 183 frame which carries SDP
        result.StateMachine!.States.Should().Contain("early_media");
        // The 183 message type should appear
        result.MessageTypes.Should().Contain("183 Session Progress");
    }

    [Fact]
    public void Analyze_returns_low_confidence_for_empty_frames()
    {
        var stream = MakeStream([], subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        result.Confidence.Should().Be(ConfidenceLevel.Low);
        result.StateMachine.Should().BeNull();
    }

    [Fact]
    public void Analyze_returns_low_confidence_when_no_sip_in_frames()
    {
        // WebSocket stream with SIP subprotocol but no SIP-parseable frames
        var stream = MakeStream(
        [
            SipFrame("""{"type":"ping"}"""),
            SipFrame("just some text"),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        result.Confidence.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void Analyze_skips_streams_with_non_sip_subprotocol()
    {
        var stream = MakeStream(
        [
            SipFrame(Invite()),
            SipFrame(Ack()),
            SipFrame(Bye()),
        ],
        subprotocol: "graphql-ws");

        var result = new SipAnalyzer().Analyze(stream);

        // Should fall through to default analysis (low confidence, no state machine)
        result.Confidence.Should().Be(ConfidenceLevel.Low);
        result.StateMachine.Should().BeNull();
    }

    [Fact]
    public void Analyze_accepts_stream_with_no_subprotocol_set()
    {
        // No subprotocol metadata at all — should attempt SIP parsing
        var stream = MakeStream(
        [
            SipFrame(Invite()),
            SipFrame(Trying()),
            SipFrame(Ack()),
            SipFrame(Bye()),
        ]);

        var result = new SipAnalyzer().Analyze(stream);

        result.StateMachine.Should().NotBeNull();
        result.SubProtocol.Should().Be("sip");
    }

    [Fact]
    public void Analyze_reports_high_confidence_with_five_or_more_messages()
    {
        var stream = MakeStream(
        [
            SipFrame(Register()),
            SipFrame(Ok200()),
            SipFrame(Invite()),
            SipFrame(Trying()),
            SipFrame(Ack()),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        result.Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_reports_medium_confidence_with_fewer_than_five_messages()
    {
        var stream = MakeStream(
        [
            SipFrame(Invite()),
            SipFrame(Ack()),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        result.Confidence.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public void Analyze_adds_limitation_for_multiple_call_ids()
    {
        var stream = MakeStream(
        [
            SipFrame(Invite("call1")),
            SipFrame(Ack("call1")),
            SipFrame(Invite("call2")),
            SipFrame(Ack("call2")),
            SipFrame(Bye("call2")),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        result.Limitations.Should().ContainMatch("*2 distinct calls*");
    }

    [Fact]
    public void CanAnalyze_returns_true_only_for_websocket()
    {
        var analyzer = new SipAnalyzer();
        analyzer.CanAnalyze(StreamProtocol.WebSocket).Should().BeTrue();
        analyzer.CanAnalyze(StreamProtocol.WebRtc).Should().BeFalse();
        analyzer.CanAnalyze(StreamProtocol.ServerSentEvents).Should().BeFalse();
    }

    [Fact]
    public void Analyze_transitions_deduplicated_in_state_machine()
    {
        // Two INVITEs produce the same "registering->inviting" arc only once
        var stream = MakeStream(
        [
            SipFrame(Register()),
            SipFrame(Invite("call1")),
            SipFrame(Ack("call1")),
            SipFrame(Register()),
            SipFrame(Invite("call2")),
            SipFrame(Ack("call2")),
        ],
        subprotocol: "sip");

        var result = new SipAnalyzer().Analyze(stream);

        var registerToInvite = result.StateMachine!.Transitions
            .Where(t => t.From == "registering" && t.To == "inviting")
            .ToList();
        registerToInvite.Should().HaveCount(1);
    }
}
