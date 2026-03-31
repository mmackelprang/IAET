using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Models;

namespace Iaet.Cookies.Tests;

public sealed class FileCookieStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly FileCookieStore _store;

    public FileCookieStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _store = new FileCookieStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task SaveSnapshot_and_GetSnapshot_round_trip()
    {
        var snapshot = MakeSnapshot("proj", "test");
        await _store.SaveSnapshotAsync(snapshot);

        var loaded = await _store.GetSnapshotAsync("proj", snapshot.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(snapshot.Id);
        loaded.Cookies.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListSnapshots_returns_all_for_project()
    {
        await _store.SaveSnapshotAsync(MakeSnapshot("proj", "snap1"));
        await _store.SaveSnapshotAsync(MakeSnapshot("proj", "snap2"));
        await _store.SaveSnapshotAsync(MakeSnapshot("other", "snap3"));

        var list = await _store.ListSnapshotsAsync("proj");

        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSnapshot_returns_null_for_nonexistent()
    {
        var result = await _store.GetSnapshotAsync("proj", Guid.NewGuid());
        result.Should().BeNull();
    }

    private static CookieSnapshotInfo MakeSnapshot(string project, string source) => new()
    {
        Id = Guid.NewGuid(),
        ProjectName = project,
        CapturedAt = DateTimeOffset.UtcNow,
        Source = source,
        Cookies = [new CapturedCookie { Name = "SID", Value = "v", Domain = ".x.com", Path = "/" }],
    };
}
