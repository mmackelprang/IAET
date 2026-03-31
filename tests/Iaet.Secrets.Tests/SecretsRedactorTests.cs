using FluentAssertions;
using Iaet.Secrets;

namespace Iaet.Secrets.Tests;

public sealed class SecretsRedactorTests : IDisposable
{
    private readonly string _rootDir;
    private readonly DotEnvSecretsStore _secretsStore;
    private readonly SecretsRedactor _redactor;

    public SecretsRedactorTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_rootDir, "proj"));
        _secretsStore = new DotEnvSecretsStore(_rootDir);
        _redactor = new SecretsRedactor(_secretsStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Redact_replaces_secret_values_with_marker()
    {
        await _secretsStore.SetAsync("proj", "TOKEN", "abc123secret");
        var result = await _redactor.RedactAsync("Authorization: Bearer abc123secret", "proj");
        result.Should().Be("Authorization: Bearer <REDACTED:TOKEN>");
    }

    [Fact]
    public async Task Redact_handles_multiple_secrets()
    {
        await _secretsStore.SetAsync("proj", "TOKEN_A", "secret1");
        await _secretsStore.SetAsync("proj", "TOKEN_B", "secret2");
        var result = await _redactor.RedactAsync("secret1 and secret2", "proj");
        result.Should().NotContain("secret1");
        result.Should().NotContain("secret2");
    }

    [Fact]
    public async Task Redact_returns_input_unchanged_when_no_secrets()
    {
        var result = await _redactor.RedactAsync("nothing to redact", "proj");
        result.Should().Be("nothing to redact");
    }

    [Fact]
    public async Task Redact_skips_short_values()
    {
        await _secretsStore.SetAsync("proj", "SHORT", "ab");
        var result = await _redactor.RedactAsync("ab is fine", "proj");
        result.Should().Be("ab is fine");
    }
}
