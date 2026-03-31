using System.Text.Json;
using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Iaet.Cookies.Tests;

public sealed class StorageScannerTests
{
    [Fact]
    public async Task ScanLocalStorage_returns_key_value_pairs()
    {
        var cdp = Substitute.For<ICdpSession>();
        var entries = JsonDocument.Parse("""
        {
            "result": {
                "type": "object",
                "value": "{\"token\":\"abc123\",\"theme\":\"dark\"}"
            }
        }
        """);
        cdp.SendCommandAsync("Runtime.evaluate",
            Arg.Is<object?>(o => o != null),
            Arg.Any<CancellationToken>())
            .Returns(entries.RootElement);

        var scanner = new StorageScanner(cdp);
        var result = await scanner.ScanLocalStorageAsync();

        result.Should().ContainKey("token");
        result["token"].Should().Be("abc123");
    }

    [Fact]
    public async Task ScanLocalStorage_returns_empty_on_error()
    {
        var cdp = Substitute.For<ICdpSession>();
        cdp.SendCommandAsync("Runtime.evaluate", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("eval failed"));

        var scanner = new StorageScanner(cdp);
        var result = await scanner.ScanLocalStorageAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectTokens_finds_jwt_and_bearer_patterns()
    {
        var storage = new Dictionary<string, string>
        {
            ["access_token"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.sig",
            ["theme"] = "dark",
            ["auth_bearer"] = "Bearer abc123",
            ["count"] = "42",
        };

        var tokens = StorageScanner.DetectTokens(storage);

        tokens.Should().ContainKey("access_token");
        tokens.Should().ContainKey("auth_bearer");
        tokens.Should().NotContainKey("theme");
        tokens.Should().NotContainKey("count");
    }
}
