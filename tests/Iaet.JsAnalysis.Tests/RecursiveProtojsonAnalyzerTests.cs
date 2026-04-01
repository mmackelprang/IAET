using FluentAssertions;
using Iaet.Core.Models;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class RecursiveProtojsonAnalyzerTests
{
    [Fact]
    public void Analyze_flat_array_with_phone_number_resolves_phoneNumber()
    {
        var json = """["+15551234567", "John", 42]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields.Should().HaveCount(3);
        fields[0].ResolvedName.Should().Be("phoneNumber");
        fields[0].Confidence.Should().Be(ConfidenceLevel.High);
        fields[0].DataType.Should().Be("string");
    }

    [Fact]
    public void Analyze_nested_array_produces_nested_fields()
    {
        var json = """["+15551234567", ["nested1", 123]]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields.Should().HaveCount(2);
        fields[1].DataType.Should().Be("array");
        fields[1].NestedFields.Should().NotBeNull();
        fields[1].NestedFields!.Should().HaveCount(2);
        fields[1].NestedFields![0].DataType.Should().Be("string");
        fields[1].NestedFields![1].DataType.Should().Be("integer");
    }

    [Fact]
    public void Analyze_detects_repeated_entity()
    {
        // Array of same-shaped arrays (3+ fields each, 2+ items)
        var json = """
            [
                "header",
                [
                    ["+15551111111", "Alice", 1, true],
                    ["+15552222222", "Bob", 2, false],
                    ["+15553333333", "Carol", 3, true]
                ]
            ]
            """;

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields.Should().HaveCount(2);
        fields[1].IsRepeatedEntity.Should().BeTrue();
        fields[1].NestedFields.Should().NotBeNull();
        fields[1].EntityTypeName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Analyze_endpoint_context_enriches_field_names()
    {
        var json = """[null, null, null]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(
            json,
            endpointPath: "/voice/v1/voiceclient/account/get");

        fields.Should().HaveCount(3);
        // All fields are null so no value_pattern evidence, but endpoint_context should provide hints
        fields[0].Evidence.Should().Contain(e => e.Source == "endpoint_context" && e.SuggestedName == "phoneNumber");
        fields[1].Evidence.Should().Contain(e => e.Source == "endpoint_context" && e.SuggestedName == "email");
        fields[2].Evidence.Should().Contain(e => e.Source == "endpoint_context" && e.SuggestedName == "displayName");
    }

    [Fact]
    public void AnalyzeMultiple_merges_samples()
    {
        var bodies = new[]
        {
            """["+15551234567", "John", 42]""",
            """["+15559876543", "Jane", 99, true]""",
        };

        var fields = RecursiveProtojsonAnalyzer.AnalyzeMultiple(bodies);

        // Should use the sample with the most fields
        fields.Should().HaveCount(4);
        fields[0].ResolvedName.Should().Be("phoneNumber");
    }

    [Fact]
    public void Analyze_empty_input_returns_empty()
    {
        RecursiveProtojsonAnalyzer.Analyze("").Should().BeEmpty();
        RecursiveProtojsonAnalyzer.Analyze("   ").Should().BeEmpty();
        RecursiveProtojsonAnalyzer.Analyze("not json").Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeMultiple_empty_list_returns_empty()
    {
        RecursiveProtojsonAnalyzer.AnalyzeMultiple([]).Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeMultiple_null_throws()
    {
        var act = () => RecursiveProtojsonAnalyzer.AnalyzeMultiple(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Analyze_max_depth_respected()
    {
        // Deeply nested arrays
        var json = """[[[["deep"]]]]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json, maxDepth: 2);

        // Should stop recursing at depth 2
        fields.Should().HaveCount(1);
        fields[0].NestedFields.Should().NotBeNull();
        // At depth 1, we get the inner array
        var level1 = fields[0].NestedFields!;
        level1.Should().HaveCount(1);
        // At depth 2 (maxDepth), recursion stops — nested fields will be empty
        level1[0].NestedFields.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_recognizes_email()
    {
        var json = """["user@example.com"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("email");
        fields[0].Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_recognizes_url()
    {
        var json = """["https://example.com/api"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("url");
        fields[0].Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_recognizes_websocket_url()
    {
        var json = """["wss://voice.google.com/ws"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("websocketUrl");
    }

    [Fact]
    public void Analyze_recognizes_sip_uri()
    {
        var json = """["sip:user@sip.example.com"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("sipUri");
    }

    [Fact]
    public void Analyze_recognizes_uuid()
    {
        var json = """["550e8400-e29b-41d4-a716-446655440000"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("id");
        fields[0].Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_recognizes_timestamp_ms()
    {
        var json = """[1711900000000]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("timestampMs");
        fields[0].Confidence.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public void Analyze_recognizes_timestamp_seconds()
    {
        var json = """[1711900000]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("timestampSec");
        fields[0].Confidence.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public void Analyze_recognizes_country_code()
    {
        var json = """["US"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("countryCode");
    }

    [Fact]
    public void Analyze_recognizes_currency_code()
    {
        var json = """["USD"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("currency");
    }

    [Fact]
    public void Analyze_non_array_root_returns_empty()
    {
        var json = """{"key": "value"}""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_boolean_field_detected()
    {
        var json = """[true]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields.Should().HaveCount(1);
        fields[0].DataType.Should().Be("boolean");
    }

    [Fact]
    public void Analyze_ip_address_resolved()
    {
        var json = """["192.168.1.1"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("ipAddress");
    }

    [Fact]
    public void Analyze_device_name_resolved()
    {
        var json = """["Web"]""";

        var fields = RecursiveProtojsonAnalyzer.Analyze(json);

        fields[0].ResolvedName.Should().Be("deviceName");
        fields[0].Confidence.Should().Be(ConfidenceLevel.High);
    }
}
