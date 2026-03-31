using System.Text.Json;
using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Abstractions;
using NSubstitute;

namespace Iaet.Cookies.Tests;

public sealed class CookieCollectorTests
{
    [Fact]
    public async Task CollectAll_parses_cdp_response()
    {
        var cdp = Substitute.For<ICdpSession>();
        var json = JsonDocument.Parse("""
        {
            "cookies": [
                {
                    "name": "SID",
                    "value": "abc123",
                    "domain": ".google.com",
                    "path": "/",
                    "expires": 1743465600,
                    "httpOnly": true,
                    "secure": true,
                    "sameSite": "Lax",
                    "size": 64
                }
            ]
        }
        """);
        cdp.SendCommandAsync("Network.getAllCookies", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(json.RootElement);

        var collector = new CookieCollector(cdp);
        var cookies = await collector.CollectAllAsync();

        cookies.Should().HaveCount(1);
        cookies[0].Name.Should().Be("SID");
        cookies[0].Domain.Should().Be(".google.com");
        cookies[0].HttpOnly.Should().BeTrue();
    }

    [Fact]
    public async Task TakeSnapshot_creates_snapshot_with_metadata()
    {
        var cdp = Substitute.For<ICdpSession>();
        var json = JsonDocument.Parse("""{"cookies": [{"name": "A", "value": "1", "domain": "x", "path": "/", "expires": 0, "httpOnly": false, "secure": false, "sameSite": "None", "size": 10}]}""");
        cdp.SendCommandAsync("Network.getAllCookies", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(json.RootElement);

        var collector = new CookieCollector(cdp);
        var snapshot = await collector.TakeSnapshotAsync("myproject", "post-login");

        snapshot.ProjectName.Should().Be("myproject");
        snapshot.Source.Should().Be("post-login");
        snapshot.Cookies.Should().HaveCount(1);
    }
}
