using System.Text;
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Native.Tests.Elf;

namespace Iaet.Native.Tests;

public sealed class NativeBinaryAnalyzerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "iaet-native-" + Guid.NewGuid().ToString("N"));

    public NativeBinaryAnalyzerTests()
    {
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public async Task Analyze_combines_static_providers_and_records_evidence()
    {
        var rodata = Encoding.ASCII.GetBytes("\0https://api.vendor.com/v2/devices\0OpenSSL 1.1.1w\0ssl3_read\0TLSv1.2\00000fff0-0000-1000-8000-00805f9b34fb\0");
        var img = ElfTestImage.Build(
        [
            new("Java_com_app_Bridge_send", Defined: true, IsFunction: true),
            new("SSL_CTX_new", Defined: false, IsFunction: true),
            new("EVP_EncryptInit", Defined: false, IsFunction: true),
        ], ["libssl.so", "libc.so"], soName: "libbridge.so", extraData: rodata);
        var path = Path.Combine(_dir, "libbridge.so");
        await File.WriteAllBytesAsync(path, img);

        var info = await NativeBinaryAnalyzer.AnalyzeAsync(path, "apk/lib/arm64-v8a/libbridge.so", "arm64-v8a",
            new NativeAnalysisOptions { UseGhidra = false });

        info.Format.Should().Be(BinaryFormat.Elf);
        info.Architecture.Should().Be("aarch64");
        info.Abi.Should().Be("arm64-v8a");
        info.NeededLibraries.Should().Equal("libssl.so", "libc.so");
        info.JniBindings.Should().ContainSingle().Which.JavaClass.Should().Be("com.app.Bridge");
        info.Urls.Should().ContainSingle().Which.Url.Should().Be("https://api.vendor.com/v2/devices");
        info.BleUuids.Should().Contain("0000FFF0-0000-1000-8000-00805F9B34FB");
        info.Libraries.Should().Contain(l => l.Library == "OpenSSL" && l.Version == "1.1.1w");
        info.Providers.Should().Equal("format-detector", "elf-reader", "strings");
        info.Functions.Should().BeEmpty();

        info.Evidence.Should().Contain(e => e.Provider == "elf-reader" && e.Kind == EvidenceKind.Observation);
        info.Evidence.Should().Contain(e => e.Provider == "library-fingerprinter" && e.Kind == EvidenceKind.Inference);
        info.Evidence.Should().OnlyContain(e => e.ArtifactSha256 == info.Sha256);
        info.Sha256.Should().HaveLength(64);
    }

    [Fact]
    public async Task Analyze_without_ghidra_installed_raises_unknown_not_silence()
    {
        var img = ElfTestImage.Build([new("helper", Defined: true, IsFunction: true)], []);
        var path = Path.Combine(_dir, "libx.so");
        await File.WriteAllBytesAsync(path, img);

        var info = await NativeBinaryAnalyzer.AnalyzeAsync(path, "native/libx.so", "armeabi-v7a",
            new NativeAnalysisOptions { UseGhidra = true, GhidraPath = Path.Combine(_dir, "no-ghidra-here") });

        info.Limitations.Should().Contain(l => l.Contains("Ghidra not available", StringComparison.Ordinal));
        info.Unknowns.Should().Contain(u => u.Id == "ghidra-missing-libx.so" && u.Status == UnknownStatus.Open);
        // No Java_* exports but there are exports → JNI registration question is raised.
        info.Unknowns.Should().Contain(u => u.Id == "jni-registration-libx.so");
    }

    [Fact]
    public async Task Analyze_handles_unknown_format()
    {
        var path = Path.Combine(_dir, "blob.bin");
        await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("just some text with https://example.com/path inside"));

        var info = await NativeBinaryAnalyzer.AnalyzeAsync(path, "native/blob.bin", null, new NativeAnalysisOptions { UseGhidra = false });

        info.Format.Should().Be(BinaryFormat.Unknown);
        info.Urls.Should().ContainSingle().Which.Confidence.Should().Be(ConfidenceLevel.Low);
        info.Evidence.First(e => e.Provider == "format-detector").Confidence.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void DiscoverApkLibraries_prefers_best_abi_per_library()
    {
        var apk = Path.Combine(_dir, "apk");
        foreach (var abi in new[] { "armeabi-v7a", "arm64-v8a", "x86" })
        {
            var d = Path.Combine(apk, "decompiled", "resources", "lib", abi);
            Directory.CreateDirectory(d);
            File.WriteAllBytes(Path.Combine(d, "libnative.so"), [0x7F]);
        }
        Directory.CreateDirectory(Path.Combine(apk, "resources", "lib", "x86"));
        File.WriteAllBytes(Path.Combine(apk, "resources", "lib", "x86", "libonlyx86.so"), [0x7F]);

        var libs = NativeBinaryAnalyzer.DiscoverApkLibraries(apk);

        libs.Should().HaveCount(2);
        libs.Single(l => l.Path.EndsWith("libnative.so", StringComparison.Ordinal)).Abi.Should().Be("arm64-v8a");
        libs.Single(l => l.Path.EndsWith("libonlyx86.so", StringComparison.Ordinal)).Abi.Should().Be("x86");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
