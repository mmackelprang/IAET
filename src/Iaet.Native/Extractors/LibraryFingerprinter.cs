using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.Native.Extractors;

/// <summary>
/// Identifies well-known third-party libraries linked into a binary from distinctive strings and
/// symbol names. This is the string-based cousin of Ghidra's Function ID: knowing that a blob
/// contains BoringSSL, protobuf or Flutter tells the investigator where the protocol logic
/// lives and which parts are boilerplate they can skip.
/// </summary>
public static partial class LibraryFingerprinter
{
    private sealed record Signature(string Library, string[] StringIndicators, string[] SymbolPrefixes, Regex? VersionPattern);

    [GeneratedRegex(@"OpenSSL (\d+\.\d+\.\d+[a-z]?)", RegexOptions.CultureInvariant)]
    private static partial Regex OpenSslVersion();

    [GeneratedRegex(@"mbed ?TLS (\d+\.\d+\.\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MbedTlsVersion();

    [GeneratedRegex(@"libcurl/(\d+\.\d+\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex CurlVersion();

    [GeneratedRegex(@"SQLite version (\d+\.\d+\.\d+)|(\b3\.\d{1,2}\.\d{1,2}\b)(?=.*sqlite)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SqliteVersion();

    [GeneratedRegex(@"zlib (?:version )?(\d+\.\d+\.\d+)|\b(1\.[23]\.\d{1,2})\b(?=.*deflate)", RegexOptions.CultureInvariant)]
    private static partial Regex ZlibVersion();

    private static readonly Signature[] Signatures =
    [
        new("OpenSSL", ["OpenSSL", "ssl3_", "TLSv1", "RSA-PSS"], ["SSL_", "EVP_", "BIO_", "X509_"], OpenSslVersion()),
        new("BoringSSL", ["BoringSSL", "boringssl"], ["SSL_CTX_set_custom_verify", "CRYPTO_BUFFER_"], null),
        new("mbedTLS", ["mbedtls", "mbed TLS", "PolarSSL"], ["mbedtls_"], MbedTlsVersion()),
        new("libcurl", ["libcurl/", "CURLOPT_"], ["curl_easy_", "curl_multi_"], CurlVersion()),
        new("SQLite", ["SQLite format 3", "sqlite_master", "sqlite3_"], ["sqlite3_"], SqliteVersion()),
        new("zlib", ["inflate", "deflate 1.", "incorrect header check"], ["inflateInit", "deflateInit", "crc32"], ZlibVersion()),
        new("Protocol Buffers", ["google/protobuf/", "protobuf", "GeneratedMessageLite"], ["_ZN6google8protobuf"], null),
        new("gRPC", ["grpc_", "grpc.", "/grpc.", "grpc-status"], ["grpc_"], null),
        new("Flutter", ["flutter_assets", "libflutter", "FlutterEngine", "dart:"], ["FlutterEngine", "Dart_"], null),
        new("React Native", ["ReactNativeJS", "libreactnativejni", "hermes", "facebook::react"], ["_ZN8facebook5react"], null),
        new("Unity", ["UnityEngine", "il2cpp", "libunity"], ["il2cpp_", "UnityMain"], null),
        new("Unreal Engine", ["UnrealEngine", "UE4Game", "UE5"], ["ANativeActivity_onCreate_UE"], null),
        new("OpenCV", ["opencv", "cv::", "libopencv"], ["_ZN2cv"], null),
        new("FFmpeg", ["libavcodec", "libavformat", "FFmpeg"], ["avcodec_", "avformat_", "av_"], null),
        new("WebRTC", ["webrtc::", "libjingle", "rtc::", "libwebrtc"], ["_ZN6webrtc"], null),
        new("Lua", ["Lua 5.", "lua_", "luaL_"], ["lua_", "luaL_"], null),
        new("Rust std", ["rustc", "/rustc/", "core::panicking", "alloc::"], ["_ZN3std", "_ZN4core"], null),
        new("Go runtime", ["go.buildid", "runtime.goexit", "Go build ID"], ["runtime.", "go.itab"], null),
        new("Qt", ["QtCore", "QObject", "libQt5", "libQt6"], ["_ZN7QObject", "_ZN7QString"], null),
        new("nlohmann/json", ["nlohmann", "json.exception"], ["_ZN8nlohmann"], null),
        new("libusb", ["libusb", "libusb_"], ["libusb_"], null),
        new("Nordic SoftDevice / nRF SDK", ["nrf_", "ble_gap_", "sd_ble_"], ["sd_ble_", "nrf_"], null),
        new("Espressif ESP-IDF", ["esp_", "ESP-IDF", "esp32"], ["esp_"], null),
    ];

    public static IReadOnlyList<LibraryFingerprint> Fingerprint(IEnumerable<ExtractedString> strings, IEnumerable<NativeSymbol> symbols)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(symbols);

        var stringValues = strings.Select(s => s.Value).ToList();
        var symbolNames = symbols.Select(s => s.Name).ToList();
        var results = new List<LibraryFingerprint>();

        foreach (var sig in Signatures)
        {
            var matched = new List<string>();
            foreach (var indicator in sig.StringIndicators)
            {
                if (stringValues.Any(v => v.Contains(indicator, StringComparison.Ordinal)))
                    matched.Add($"string:{indicator}");
            }
            foreach (var prefix in sig.SymbolPrefixes)
            {
                if (symbolNames.Any(n => n.StartsWith(prefix, StringComparison.Ordinal)))
                    matched.Add($"symbol:{prefix}*");
            }
            if (matched.Count == 0)
                continue;

            string? version = null;
            if (sig.VersionPattern is not null)
            {
                foreach (var v in stringValues)
                {
                    var m = sig.VersionPattern.Match(v);
                    if (!m.Success)
                        continue;
                    version = m.Groups.Cast<Group>().Skip(1).FirstOrDefault(g => g.Success)?.Value;
                    if (version is not null)
                        break;
                }
            }

            var confidence = matched.Count >= 3 || version is not null ? ConfidenceLevel.High
                : matched.Count == 2 ? ConfidenceLevel.Medium
                : ConfidenceLevel.Low;

            results.Add(new LibraryFingerprint
            {
                Library = sig.Library,
                Version = version,
                Confidence = confidence,
                MatchedIndicators = matched,
            });
        }

        return results;
    }
}
