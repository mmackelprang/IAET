using FluentAssertions;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class SipMessageParserTests
{
    [Fact]
    public void TryParse_parses_sip_request()
    {
        var text = "INVITE sip:+19193718044@web.c.pbx.voice.sip.google.com SIP/2.0\r\n" +
                   "Via: SIP/2.0/wss example.invalid;branch=z9hG4bK1234\r\n" +
                   "Call-ID: abc123@example\r\n" +
                   "CSeq: 1 INVITE\r\n" +
                   "From: <sip:user@example.com>;tag=xyz\r\n" +
                   "To: <sip:+19193718044@google.com>\r\n" +
                   "Content-Type: application/sdp\r\n" +
                   "\r\n" +
                   "v=0\r\n" +
                   "o=- 123 456 IN IP4 0.0.0.0\r\n";

        var msg = SipMessageParser.TryParse(text);

        msg.Should().NotBeNull();
        msg!.IsRequest.Should().BeTrue();
        msg.Method.Should().Be("INVITE");
        msg.RequestUri.Should().Contain("+19193718044");
        msg.CallId.Should().Be("abc123@example");
        msg.CSeq.Should().Be("1 INVITE");
        msg.ContentType.Should().Contain("sdp");
        msg.Body.Should().Contain("v=0");
    }

    [Fact]
    public void TryParse_parses_sip_response()
    {
        var text = "SIP/2.0 200 OK\r\n" +
                   "Call-ID: abc123@example\r\n" +
                   "CSeq: 1 INVITE\r\n" +
                   "\r\n";

        var msg = SipMessageParser.TryParse(text);

        msg.Should().NotBeNull();
        msg!.IsRequest.Should().BeFalse();
        msg.StatusCode.Should().Be(200);
        msg.ReasonPhrase.Should().Be("OK");
    }

    [Fact]
    public void TryParse_parses_183_session_progress()
    {
        var text = "SIP/2.0 183 Session Progress\r\n" +
                   "Call-ID: call1\r\n" +
                   "\r\n";

        var msg = SipMessageParser.TryParse(text);

        msg.Should().NotBeNull();
        msg!.StatusCode.Should().Be(183);
        msg.ReasonPhrase.Should().Be("Session Progress");
    }

    [Fact]
    public void TryParse_returns_null_for_empty()
    {
        SipMessageParser.TryParse("").Should().BeNull();
        SipMessageParser.TryParse(null!).Should().BeNull();
    }

    [Fact]
    public void TryParse_returns_null_for_non_sip()
    {
        SipMessageParser.TryParse("just some random text").Should().BeNull();
    }

    [Fact]
    public void TryParse_extracts_register()
    {
        var text = "REGISTER sip:web.c.pbx.voice.sip.google.com SIP/2.0\r\n" +
                   "Via: SIP/2.0/wss example.invalid\r\n" +
                   "Call-ID: reg1\r\n" +
                   "\r\n";

        var msg = SipMessageParser.TryParse(text);

        msg!.Method.Should().Be("REGISTER");
        msg.CallId.Should().Be("reg1");
    }

    [Fact]
    public void BuildTimeline_creates_ordered_entries()
    {
        var messages = new List<SipMessage>
        {
            new() { IsRequest = true, Method = "REGISTER", CallId = "r1" },
            new() { IsRequest = false, StatusCode = 200, ReasonPhrase = "OK", CallId = "r1" },
            new() { IsRequest = true, Method = "INVITE", CallId = "c1", ContentType = "application/sdp", Body = "v=0\n..." },
            new() { IsRequest = false, StatusCode = 100, ReasonPhrase = "Trying", CallId = "c1" },
            new() { IsRequest = false, StatusCode = 183, ReasonPhrase = "Session Progress", CallId = "c1", ContentType = "application/sdp", Body = "v=0\n..." },
            new() { IsRequest = true, Method = "ACK", CallId = "c1" },
            new() { IsRequest = true, Method = "BYE", CallId = "c1" },
        };

        var timeline = SipMessageParser.BuildTimeline(messages);

        timeline.Should().HaveCount(7);
        timeline[0].Label.Should().Be("REGISTER");
        timeline[2].Label.Should().Be("INVITE");
        timeline[2].HasSdp.Should().BeTrue();
        timeline[4].HasSdp.Should().BeTrue();
        timeline[6].Label.Should().Be("BYE");
    }
}
