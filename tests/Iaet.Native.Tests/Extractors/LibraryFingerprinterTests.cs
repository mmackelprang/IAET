using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Native.Extractors;

namespace Iaet.Native.Tests.Extractors;

public sealed class LibraryFingerprinterTests
{
    private static ExtractedString S(string v) => new(v, 0, "ascii");

    [Fact]
    public void Fingerprint_detects_openssl_with_version()
    {
        var strings = new[] { S("OpenSSL 1.1.1w  11 Sep 2023"), S("TLSv1.3"), S("ssl3_read_bytes") };
        var symbols = new[] { new NativeSymbol { Name = "SSL_CTX_new" }, new NativeSymbol { Name = "EVP_DigestInit" } };

        var libs = LibraryFingerprinter.Fingerprint(strings, symbols);

        var ssl = libs.Should().ContainSingle(l => l.Library == "OpenSSL").Subject;
        ssl.Version.Should().Be("1.1.1w");
        ssl.Confidence.Should().Be(ConfidenceLevel.High);
        ssl.MatchedIndicators.Should().Contain("symbol:SSL_*");
    }

    [Fact]
    public void Fingerprint_detects_flutter_and_protobuf()
    {
        var strings = new[] { S("flutter_assets/kernel_blob.bin"), S("google/protobuf/descriptor.proto"), S("GeneratedMessageLite") };

        var libs = LibraryFingerprinter.Fingerprint(strings, []);

        libs.Select(l => l.Library).Should().Contain("Flutter").And.Contain("Protocol Buffers");
        // Three protobuf indicators match (path, name, generated-message class) → high confidence.
        libs.Single(l => l.Library == "Protocol Buffers").Confidence.Should().Be(ConfidenceLevel.High);
        libs.Single(l => l.Library == "Flutter").Confidence.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void Fingerprint_single_indicator_is_low_confidence()
    {
        var libs = LibraryFingerprinter.Fingerprint([S("Lua 5.4.6  Copyright (C) 1994-2023 Lua.org, PUC-Rio")], []);

        libs.Should().ContainSingle(l => l.Library == "Lua").Which.Confidence.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void Fingerprint_returns_empty_when_nothing_matches()
    {
        LibraryFingerprinter.Fingerprint([S("hello world"), S("nothing to see")], []).Should().BeEmpty();
    }
}
