using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class CoverageReportGeneratorTests
{
    [Fact]
    public void Generate_includes_observed_endpoints()
    {
        var ctx = TestContextFactory.MakeContext();

        var report = CoverageReportGenerator.Generate(ctx);

        report.Should().Contain("Coverage Report");
        report.Should().Contain("GET /api/users/{id}");
        report.Should().Contain("Observed");
    }

    [Fact]
    public void Generate_with_known_urls_shows_coverage_percentage()
    {
        var ctx = TestContextFactory.MakeContext();
        var knownUrls = new List<ExtractedUrl>
        {
            new() { Url = "/api/users/{id}", Confidence = ConfidenceLevel.High },
            new() { Url = "/api/unknown", Confidence = ConfidenceLevel.Low },
            new() { Url = "/api/secret", Confidence = ConfidenceLevel.Medium },
        };

        var report = CoverageReportGenerator.Generate(ctx, knownUrls);

        report.Should().Contain("Coverage:");
        report.Should().Contain("/api/unknown");
        report.Should().Contain("Not observed");
    }

    [Fact]
    public void Generate_handles_empty_context()
    {
        var ctx = TestContextFactory.MakeEmptyContext();

        var report = CoverageReportGenerator.Generate(ctx);

        report.Should().Contain("Coverage Report");
        report.Should().Contain("0 endpoints");
    }
}
