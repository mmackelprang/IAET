using System.Text.Json;
using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public class HarGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidHarJson()
    {
        var ctx  = TestContextFactory.MakeContext();
        var json = HarGenerator.Generate(ctx);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("log", out var log).Should().BeTrue();
        log.GetProperty("version").GetString().Should().Be("1.2");
    }

    [Fact]
    public void Generate_ContainsEntries()
    {
        var ctx  = TestContextFactory.MakeContext();
        var json = HarGenerator.Generate(ctx);

        var doc     = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("log").GetProperty("entries");
        entries.GetArrayLength().Should().Be(2); // two requests in test context
    }

    [Fact]
    public void Generate_TimingsCorrect()
    {
        var ctx  = TestContextFactory.MakeContext();
        var json = HarGenerator.Generate(ctx);

        var doc     = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("log").GetProperty("entries");

        // First request has DurationMs = 42
        var first    = entries[0];
        var time     = first.GetProperty("time").GetDouble();
        var waitTime = first.GetProperty("timings").GetProperty("wait").GetDouble();

        time.Should().Be(42);
        waitTime.Should().Be(42);
    }
}
