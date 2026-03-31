using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Models;

namespace Iaet.Cookies.Tests;

public sealed class CookieLifecycleAnalyzerTests
{
    [Fact]
    public void Analyze_reports_total_count()
    {
        var snapshot = MakeSnapshot([MakeCookie("A"), MakeCookie("B"), MakeCookie("C")]);

        var result = CookieLifecycleAnalyzer.Analyze("proj", snapshot);

        result.TotalCookies.Should().Be(3);
    }

    [Fact]
    public void Analyze_detects_expiring_cookies()
    {
        var soon = DateTimeOffset.UtcNow.AddMinutes(20);
        var later = DateTimeOffset.UtcNow.AddDays(30);
        var snapshot = MakeSnapshot([
            MakeCookie("EXPIRING", expires: soon),
            MakeCookie("SAFE", expires: later),
        ]);

        var result = CookieLifecycleAnalyzer.Analyze("proj", snapshot, expiryThreshold: TimeSpan.FromHours(1));

        result.ExpiringWithin.Should().ContainKey("EXPIRING");
        result.ExpiringWithin.Should().NotContainKey("SAFE");
    }

    [Fact]
    public void Analyze_detects_rotation_across_snapshots()
    {
        var snap1 = MakeSnapshot([MakeCookie("SID", value: "v1"), MakeCookie("STATIC", value: "same")]);
        var snap2 = MakeSnapshot([MakeCookie("SID", value: "v2"), MakeCookie("STATIC", value: "same")]);
        var snap3 = MakeSnapshot([MakeCookie("SID", value: "v3"), MakeCookie("STATIC", value: "same")]);

        var result = CookieLifecycleAnalyzer.AnalyzeRotation("proj", [snap1, snap2, snap3]);

        result.RotationDetected.Should().Contain("SID");
        result.RotationDetected.Should().NotContain("STATIC");
    }

    [Fact]
    public void Analyze_handles_no_expiry()
    {
        var snapshot = MakeSnapshot([MakeCookie("SESSION", expires: null)]);

        var result = CookieLifecycleAnalyzer.Analyze("proj", snapshot);

        result.ExpiringWithin.Should().BeEmpty();
    }

    private static CookieSnapshotInfo MakeSnapshot(IReadOnlyList<CapturedCookie> cookies) => new()
    {
        Id = Guid.NewGuid(),
        ProjectName = "proj",
        CapturedAt = DateTimeOffset.UtcNow,
        Source = "test",
        Cookies = cookies,
    };

    private static CapturedCookie MakeCookie(string name, string value = "val", DateTimeOffset? expires = null, string domain = ".example.com") => new()
    {
        Name = name, Value = value, Domain = domain, Path = "/",
        Expires = expires,
    };
}
