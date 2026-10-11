using FluentAssertions;

namespace Iaet.Native.Tests;

public sealed class NativeAnalysisCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "iaet-cache-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ComputeKey_is_deterministic_and_sensitive_to_every_input()
    {
        var a = NativeAnalysisCache.ComputeKey("sha", "ghidra-export", "1", "decompile=false");
        var b = NativeAnalysisCache.ComputeKey("sha", "ghidra-export", "1", "decompile=false");
        a.Should().Be(b);
        a.Should().HaveLength(64);

        NativeAnalysisCache.ComputeKey("sha2", "ghidra-export", "1", "decompile=false").Should().NotBe(a);
        NativeAnalysisCache.ComputeKey("sha", "other", "1", "decompile=false").Should().NotBe(a);
        NativeAnalysisCache.ComputeKey("sha", "ghidra-export", "2", "decompile=false").Should().NotBe(a);
        NativeAnalysisCache.ComputeKey("sha", "ghidra-export", "1", "decompile=true").Should().NotBe(a);
    }

    [Fact]
    public async Task Put_then_TryGet_roundtrips()
    {
        var cache = new NativeAnalysisCache(_dir);
        var key = NativeAnalysisCache.ComputeKey("abc", "op", "1", "p");

        cache.TryGet(key, out _).Should().BeFalse();
        await cache.PutAsync(key, """{"ok":true}""");

        cache.TryGet(key, out var json).Should().BeTrue();
        json.Should().Be("""{"ok":true}""");
    }

    [Fact]
    public async Task HashFile_matches_sha256_of_contents()
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "x.bin");
        await File.WriteAllBytesAsync(file, "hello"u8.ToArray());

        var hash = await NativeAnalysisCache.HashFileAsync(file);

        hash.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
