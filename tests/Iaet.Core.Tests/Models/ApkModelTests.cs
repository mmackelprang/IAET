using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class ApkModelTests
{
    [Fact]
    public void ApkInfo_holds_package_metadata()
    {
        var info = new ApkInfo
        {
            PackageName = "com.google.android.apps.voice",
            VersionName = "5.20.1",
            VersionCode = 520100,
            MinSdk = 24,
            TargetSdk = 34,
            Permissions = ["android.permission.INTERNET", "android.permission.BLUETOOTH"],
            ExportedServices = ["com.google.voice.SipService"],
        };

        info.PackageName.Should().Be("com.google.android.apps.voice");
        info.Permissions.Should().HaveCount(2);
        info.ExportedServices.Should().HaveCount(1);
    }

    [Fact]
    public void NetworkSecurityConfig_holds_pinning_and_cleartext()
    {
        var config = new NetworkSecurityConfig
        {
            PinnedDomains = [new PinnedDomain { Domain = "api.example.com", Pins = ["sha256/abc123"] }],
            CleartextPermittedDomains = ["debug.example.com"],
            CleartextDefaultPermitted = false,
        };

        config.PinnedDomains.Should().HaveCount(1);
        config.CleartextPermittedDomains.Should().Contain("debug.example.com");
        config.CleartextDefaultPermitted.Should().BeFalse();
    }
}
