using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public class JsonSchemaInferrerTests
{
    [Fact]
    public async Task InferAsync_MultipleBodies_ProducesAllOutputs()
    {
        var inferrer = new JsonSchemaInferrer();
        var bodies = new[]
        {
            """{"name":"Alice","age":30}""",
            """{"name":"Bob","age":25}""",
        };

        var result = await inferrer.InferAsync(bodies);

        result.JsonSchema.Should().Contain("\"$schema\"");
        result.JsonSchema.Should().Contain("\"properties\"");
        result.CSharpRecord.Should().Contain("public sealed record InferredResponse");
        result.OpenApiFragment.Should().Contain("type: object");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task InferAsync_TypeConflict_ReportsWarning()
    {
        var inferrer = new JsonSchemaInferrer();
        var bodies = new[]
        {
            """{"value":"hello"}""",
            """{"value":42}""",
        };

        var result = await inferrer.InferAsync(bodies);

        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("value");
    }

    [Fact]
    public async Task InferAsync_EmptyInput_ReturnsEmptySchema()
    {
        var inferrer = new JsonSchemaInferrer();
        var bodies = Array.Empty<string>();

        var result = await inferrer.InferAsync(bodies);

        result.JsonSchema.Should().Be("{}");
        result.CSharpRecord.Should().BeEmpty();
        result.OpenApiFragment.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }
}
