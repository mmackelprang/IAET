using FluentAssertions;
using Iaet.Secrets;

namespace Iaet.Secrets.Tests;

public sealed class DotEnvSecretsStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly DotEnvSecretsStore _store;

    public DotEnvSecretsStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_rootDir, "proj"));
        _store = new DotEnvSecretsStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Get_returns_null_when_no_env_file()
    {
        var result = await _store.GetAsync("proj", "MY_KEY");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Set_and_Get_round_trip()
    {
        await _store.SetAsync("proj", "MY_TOKEN", "secret123");
        var result = await _store.GetAsync("proj", "MY_TOKEN");
        result.Should().Be("secret123");
    }

    [Fact]
    public async Task Set_overwrites_existing_key()
    {
        await _store.SetAsync("proj", "KEY", "old");
        await _store.SetAsync("proj", "KEY", "new");
        var result = await _store.GetAsync("proj", "KEY");
        result.Should().Be("new");
    }

    [Fact]
    public async Task List_returns_all_keys()
    {
        await _store.SetAsync("proj", "A", "1");
        await _store.SetAsync("proj", "B", "2");
        var all = await _store.ListAsync("proj");
        all.Should().HaveCount(2);
        all["A"].Should().Be("1");
        all["B"].Should().Be("2");
    }

    [Fact]
    public async Task Remove_deletes_key()
    {
        await _store.SetAsync("proj", "REMOVE_ME", "val");
        await _store.RemoveAsync("proj", "REMOVE_ME");
        var result = await _store.GetAsync("proj", "REMOVE_ME");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Ignores_comments_and_blank_lines()
    {
        var envPath = Path.Combine(_rootDir, "proj", ".env.iaet");
        await File.WriteAllTextAsync(envPath, "# comment\n\nKEY=value\n");
        var result = await _store.GetAsync("proj", "KEY");
        result.Should().Be("value");
    }
}
