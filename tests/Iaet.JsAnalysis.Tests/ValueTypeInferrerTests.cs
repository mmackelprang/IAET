using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class ValueTypeInferrerTests
{
    [Fact]
    public void Infer_detects_email_field()
    {
        var bodies = new[]
        {
            """[null, "user@example.com", 42]""",
            """[null, "admin@test.org", 99]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[1].SemanticType.Should().Be("email");
        fields[1].SuggestedName.Should().Be("email");
        fields[1].Confidence.Should().Be(Iaet.Core.Models.ConfidenceLevel.High);
    }

    [Fact]
    public void Infer_detects_phone_number()
    {
        var bodies = new[]
        {
            """["+19196706660"]""",
            """["+14155551234"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("phone_number");
    }

    [Fact]
    public void Infer_detects_url()
    {
        var bodies = new[]
        {
            """["https://example.com/avatar.png"]""",
            """["https://cdn.example.com/photo.jpg"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("url");
    }

    [Fact]
    public void Infer_detects_uuid()
    {
        var bodies = new[]
        {
            """["f5260658-edce-0d06-008f-cf842c7867a4"]""",
            """["a1b2c3d4-e5f6-7890-abcd-ef1234567890"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("uuid");
        fields[0].SuggestedName.Should().Be("id");
    }

    [Fact]
    public void Infer_detects_timestamp()
    {
        var bodies = new[]
        {
            """["2026-03-31T14:00:00Z"]""",
            """["2026-04-01T10:30:00Z"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("timestamp");
    }

    [Fact]
    public void Infer_detects_boolean()
    {
        var bodies = new[]
        {
            """[true]""",
            """[false]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("boolean");
    }

    [Fact]
    public void Infer_detects_enum_value()
    {
        var bodies = new[]
        {
            """[1]""",
            """[2]""",
            """[3]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("enum_value");
    }

    [Fact]
    public void Infer_detects_unix_timestamp_ms()
    {
        var bodies = new[]
        {
            """[1774987032761]""",
            """[1774987045000]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("timestamp_ms");
    }

    [Fact]
    public void Infer_detects_base64_encoded_data()
    {
        var bodies = new[]
        {
            """["CiRmNTI2MDY1OC1lZGNlLTBkMDYtMDA4Zi1jZjg0MmM3ODY3YTQ="]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("encoded_data");
    }

    [Fact]
    public void Infer_detects_currency()
    {
        var bodies = new[]
        {
            """["USD"]""",
            """["EUR"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("currency");
    }

    [Fact]
    public void Infer_handles_null_positions()
    {
        var bodies = new[]
        {
            """[null, "hello"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("null");
        fields[1].SemanticType.Should().Be("identifier");
    }

    [Fact]
    public void Infer_detects_ip_address()
    {
        var bodies = new[]
        {
            """["192.168.1.1"]""",
            """["10.0.0.255"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().Be("ip_address");
        fields[0].SuggestedName.Should().Be("ipAddress");
        fields[0].Confidence.Should().Be(Iaet.Core.Models.ConfidenceLevel.Medium);
    }

    [Fact]
    public void Infer_does_not_classify_ip_as_phone()
    {
        var bodies = new[]
        {
            """["192.168.1.1"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SemanticType.Should().NotBe("phone_number");
    }

    [Fact]
    public void Infer_handles_empty_input()
    {
        ValueTypeInferrer.InferFromSamples([]).Should().BeEmpty();
    }

    [Fact]
    public void Infer_handles_mixed_types_across_samples()
    {
        var bodies = new[]
        {
            """[null, "text"]""",
            """["value", "text"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        // Position 0 has null in one sample, string in another
        fields.Should().HaveCount(2);
    }

    [Fact]
    public void Infer_keeps_sample_values_for_reference()
    {
        var bodies = new[]
        {
            """["user@example.com"]""",
        };
        var fields = ValueTypeInferrer.InferFromSamples(bodies);
        fields[0].SampleValues.Should().Contain("user@example.com");
    }
}
