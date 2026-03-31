using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class WebSocketUrlExtractorTests
{
    [Fact]
    public void Extract_finds_websocket_constructor_urls()
    {
        var js = """
            const ws = new WebSocket("wss://voice.google.com/signal");
            const ws2 = new WebSocket('ws://localhost:8080/ws');
            """;

        var urls = WebSocketUrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "wss://voice.google.com/signal");
        urls.Should().Contain(u => u.Url == "ws://localhost:8080/ws");
    }

    [Fact]
    public void Extract_handles_no_websockets()
    {
        WebSocketUrlExtractor.Extract("var x = 1;", "bundle.js").Should().BeEmpty();
    }
}
