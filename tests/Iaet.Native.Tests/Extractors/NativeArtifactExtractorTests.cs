using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Native.Extractors;

namespace Iaet.Native.Tests.Extractors;

public sealed class NativeArtifactExtractorTests
{
    private static ExtractedString S(string v, long off = 0) => new(v, off, "ascii");

    [Fact]
    public void ExtractUrls_classifies_confidence()
    {
        var strings = new[]
        {
            S("https://api.vendor.com/v2/devices"),
            S("https://cdn.vendor.com/fw/%s/manifest.json"),
            S("http://www.w3.org/2001/XMLSchema"),
            S("wss://rt.vendor.com/socket"),
            S("https://api.vendor.com/v2/devices"), // duplicate
        };

        var urls = NativeArtifactExtractor.ExtractUrls(strings, "native/libfoo.so");

        urls.Should().HaveCount(4);
        urls.Single(u => u.Url == "https://api.vendor.com/v2/devices").Confidence.Should().Be(ConfidenceLevel.High);
        urls.Single(u => u.Url.Contains("%s", StringComparison.Ordinal)).Confidence.Should().Be(ConfidenceLevel.Medium);
        urls.Single(u => u.Url.Contains("w3.org", StringComparison.Ordinal)).Confidence.Should().Be(ConfidenceLevel.Low);
        urls.Should().OnlyContain(u => u.SourceFile == "native/libfoo.so");
    }

    [Fact]
    public void ExtractBleUuids_normalizes_and_detects_base_uuids()
    {
        var strings = new[]
        {
            S("service 0000fff0-0000-1000-8000-00805f9b34fb"),
            S("6E400001-B5A3-F393-E0A9-E50E24DCCA9E"),
            S("not-a-uuid"),
        };

        var uuids = NativeArtifactExtractor.ExtractBleUuids(strings);

        uuids.Should().HaveCount(2);
        uuids.Should().Contain("0000FFF0-0000-1000-8000-00805F9B34FB");
        NativeArtifactExtractor.IsBluetoothBaseUuid("0000FFF0-0000-1000-8000-00805F9B34FB").Should().BeTrue();
        NativeArtifactExtractor.IsBluetoothBaseUuid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E").Should().BeFalse();
    }

    [Fact]
    public void ExtractJniBindings_decodes_only_java_exports()
    {
        var exports = new[]
        {
            new NativeSymbol { Name = "Java_com_app_Radio_setFrequency", Type = "function" },
            new NativeSymbol { Name = "JNI_OnLoad", Type = "function" },
            new NativeSymbol { Name = "helper", Type = "function" },
        };

        var bindings = NativeArtifactExtractor.ExtractJniBindings(exports);

        bindings.Should().ContainSingle().Which.JavaMethod.Should().Be("setFrequency");
    }
}
