using FluentAssertions;
using Iaet.Core.Utilities;

namespace Iaet.Core.Tests.Utilities;

public sealed class DnsResolverTests
{
    [Fact]
    public async Task Resolve_returns_ip_unchanged_for_invalid()
    {
        var result = await DnsResolver.ResolveAsync("not-an-ip");
        result.Should().Be("not-an-ip");
    }

    [Fact]
    public async Task Resolve_returns_ip_for_empty()
    {
        var result = await DnsResolver.ResolveAsync("");
        result.Should().Be("");
    }

    [Fact]
    public async Task ResolveAllInText_finds_ips_in_text()
    {
        var text = "Server at 8.8.8.8 and also 1.1.1.1 reachable";
        var results = await DnsResolver.ResolveAllInTextAsync(text);
        results.Should().ContainKey("8.8.8.8");
        results.Should().ContainKey("1.1.1.1");
    }

    [Fact]
    public async Task ResolveAllInText_skips_localhost_and_zero()
    {
        var text = "Local 127.0.0.1 and zero 0.0.0.0 should be skipped but 8.8.8.8 kept";
        var results = await DnsResolver.ResolveAllInTextAsync(text);
        results.Should().NotContainKey("127.0.0.1");
        results.Should().NotContainKey("0.0.0.0");
        results.Should().ContainKey("8.8.8.8");
    }

    [Fact]
    public async Task ResolveAllInText_handles_empty()
    {
        var results = await DnsResolver.ResolveAllInTextAsync("");
        results.Should().BeEmpty();
    }
}
