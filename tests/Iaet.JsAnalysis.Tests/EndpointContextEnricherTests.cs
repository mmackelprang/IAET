using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class EndpointContextEnricherTests
{
    [Fact]
    public void GetDomainHints_account_endpoint_returns_account_hints()
    {
        var hints = EndpointContextEnricher.GetDomainHints("/voice/v1/voiceclient/account/get");

        hints.Should().NotBeEmpty();
        hints[0].Should().Be("phoneNumber");
        hints[1].Should().Be("email");
    }

    [Fact]
    public void GetDomainHints_sip_endpoint_returns_sip_hints()
    {
        var hints = EndpointContextEnricher.GetDomainHints("/voice/v1/voiceclient/sipregisterinfo/get");

        hints.Should().NotBeEmpty();
        hints[0].Should().Be("credentials");
        hints[1].Should().Be("sipServer");
        hints[2].Should().Be("websocketUrl");
    }

    [Fact]
    public void GetDomainHints_rpc_style_endpoint_matches_by_partial()
    {
        var hints = EndpointContextEnricher.GetDomainHints(
            "/$rpc/google.internal.communications.instantmessaging.v1.Messaging/SendMessage");

        hints.Should().NotBeEmpty();
        hints[0].Should().Be("messageId");
    }

    [Fact]
    public void GetDomainHints_unknown_endpoint_returns_empty()
    {
        var hints = EndpointContextEnricher.GetDomainHints("/api/v1/something/entirely/unknown");

        hints.Should().BeEmpty();
    }

    [Fact]
    public void GetDomainHints_thread_endpoint_returns_thread_hints()
    {
        var hints = EndpointContextEnricher.GetDomainHints("/api/thread/list");

        hints.Should().NotBeEmpty();
        hints[0].Should().Be("threadId");
    }

    [Fact]
    public void GetDomainHints_call_endpoint_returns_call_hints()
    {
        var hints = EndpointContextEnricher.GetDomainHints("/voice/v1/voiceclient/call/get");

        hints.Should().NotBeEmpty();
        hints[0].Should().Be("callId");
    }

    [Fact]
    public void GetDomainHints_case_insensitive_matching()
    {
        var hints = EndpointContextEnricher.GetDomainHints("/api/Account/get");

        hints.Should().NotBeEmpty();
        hints[0].Should().Be("phoneNumber");
    }

    [Fact]
    public void GetDomainHints_null_throws()
    {
        var act = () => EndpointContextEnricher.GetDomainHints(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
