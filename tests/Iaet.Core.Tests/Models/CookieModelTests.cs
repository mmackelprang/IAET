using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class CookieModelTests
{
    [Fact]
    public void CapturedCookie_holds_full_metadata()
    {
        var cookie = new CapturedCookie
        {
            Name = "SID",
            Domain = ".google.com",
            Path = "/",
            Value = "abc123",
            Expires = DateTimeOffset.UtcNow.AddHours(1),
            HttpOnly = true,
            Secure = true,
            SameSite = "Lax",
            Size = 128,
        };

        cookie.Name.Should().Be("SID");
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
    }

    [Fact]
    public void CookieSnapshotInfo_captures_point_in_time()
    {
        var snapshot = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(),
            ProjectName = "google-voice",
            CapturedAt = DateTimeOffset.UtcNow,
            Source = "post-login",
            Cookies = [new CapturedCookie { Name = "SID", Domain = ".google.com", Path = "/", Value = "x" }],
        };

        snapshot.Cookies.Should().HaveCount(1);
        snapshot.Source.Should().Be("post-login");
    }

    [Fact]
    public void CookieDiff_tracks_added_removed_changed()
    {
        var diff = new CookieDiff
        {
            BeforeSnapshotId = Guid.NewGuid(),
            AfterSnapshotId = Guid.NewGuid(),
            Added = [new CapturedCookie { Name = "NEW", Domain = "x", Path = "/", Value = "v" }],
            Removed = [new CapturedCookie { Name = "OLD", Domain = "x", Path = "/", Value = "v" }],
            Changed = [new CookieChange { Name = "SID", Domain = ".google.com", OldValue = "a", NewValue = "b" }],
        };

        diff.Added.Should().HaveCount(1);
        diff.Removed.Should().HaveCount(1);
        diff.Changed.Should().HaveCount(1);
    }

    [Fact]
    public void CookieAnalysis_reports_auth_critical_and_expiry()
    {
        var analysis = new CookieAnalysis
        {
            ProjectName = "gv",
            TotalCookies = 38,
            AuthCritical = ["SID", "HSID"],
            ExpiringWithin = new Dictionary<string, TimeSpan>
            {
                ["SID"] = TimeSpan.FromMinutes(30),
            },
            RotationDetected = ["APISID"],
        };

        analysis.AuthCritical.Should().HaveCount(2);
        analysis.RotationDetected.Should().Contain("APISID");
    }

    [Fact]
    public void CaptureContext_annotates_trigger()
    {
        var ctx = new CaptureContext
        {
            Trigger = "click",
            ElementSelector = "button.call-btn",
            Description = "User clicked Call button",
        };

        ctx.Trigger.Should().Be("click");
    }
}
