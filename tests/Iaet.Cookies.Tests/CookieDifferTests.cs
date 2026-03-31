using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Models;

namespace Iaet.Cookies.Tests;

public sealed class CookieDifferTests
{
    [Fact]
    public void Diff_detects_added_cookies()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "val1")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "val1"), MakeCookie("NEW", "val2")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Added.Should().HaveCount(1);
        diff.Added[0].Name.Should().Be("NEW");
        diff.Removed.Should().BeEmpty();
        diff.Changed.Should().BeEmpty();
    }

    [Fact]
    public void Diff_detects_removed_cookies()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "val1"), MakeCookie("OLD", "val2")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "val1")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Removed.Should().HaveCount(1);
        diff.Removed[0].Name.Should().Be("OLD");
    }

    [Fact]
    public void Diff_detects_changed_values()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "old-value")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "new-value")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Changed.Should().HaveCount(1);
        diff.Changed[0].OldValue.Should().Be("old-value");
        diff.Changed[0].NewValue.Should().Be("new-value");
    }

    [Fact]
    public void Diff_uses_name_plus_domain_as_key()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "v1", ".google.com"), MakeCookie("SID", "v2", ".youtube.com")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "v1", ".google.com"), MakeCookie("SID", "v2-changed", ".youtube.com")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Changed.Should().HaveCount(1);
        diff.Changed[0].Domain.Should().Be(".youtube.com");
    }

    [Fact]
    public void Diff_handles_empty_snapshots()
    {
        var empty = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [],
        };

        var diff = CookieDiffer.Diff(empty, empty);

        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
        diff.Changed.Should().BeEmpty();
    }

    private static CapturedCookie MakeCookie(string name, string value, string domain = ".example.com") => new()
    {
        Name = name, Value = value, Domain = domain, Path = "/",
    };
}
