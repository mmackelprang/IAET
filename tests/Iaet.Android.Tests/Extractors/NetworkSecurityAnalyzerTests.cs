using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class NetworkSecurityAnalyzerTests
{
    [Fact]
    public void Parse_extracts_pinned_domains()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <network-security-config>
                <domain-config>
                    <domain includeSubdomains="true">api.example.com</domain>
                    <pin-set>
                        <pin digest="SHA-256">abc123def456</pin>
                        <pin digest="SHA-256">xyz789ghi012</pin>
                    </pin-set>
                </domain-config>
            </network-security-config>
            """;

        var config = NetworkSecurityAnalyzer.Parse(xml);

        config.PinnedDomains.Should().HaveCount(1);
        config.PinnedDomains[0].Domain.Should().Be("api.example.com");
        config.PinnedDomains[0].Pins.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_extracts_cleartext_policy()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <network-security-config>
                <base-config cleartextTrafficPermitted="false" />
                <domain-config cleartextTrafficPermitted="true">
                    <domain>debug.example.com</domain>
                </domain-config>
            </network-security-config>
            """;

        var config = NetworkSecurityAnalyzer.Parse(xml);

        config.CleartextDefaultPermitted.Should().BeFalse();
        config.CleartextPermittedDomains.Should().Contain("debug.example.com");
    }

    [Fact]
    public void Parse_handles_empty()
    {
        var config = NetworkSecurityAnalyzer.Parse("");

        config.PinnedDomains.Should().BeEmpty();
        config.CleartextDefaultPermitted.Should().BeTrue();
    }
}
