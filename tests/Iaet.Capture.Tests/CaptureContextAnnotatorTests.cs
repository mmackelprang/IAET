using FluentAssertions;
using Iaet.Capture;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests;

public sealed class CaptureContextAnnotatorTests
{
    [Fact]
    public void Annotate_tags_request_with_context()
    {
        var annotator = new CaptureContextAnnotator();
        var ctx = new CaptureContext
        {
            Trigger = "click",
            ElementSelector = "button.submit",
            Description = "Submit form",
        };

        annotator.SetContext(ctx);
        var request = MakeRequest();
        var annotated = annotator.Annotate(request);

        annotated.Tag.Should().Contain("click");
        annotated.Tag.Should().Contain("button.submit");
    }

    [Fact]
    public void Annotate_returns_request_unchanged_when_no_context()
    {
        var annotator = new CaptureContextAnnotator();
        var request = MakeRequest();
        var annotated = annotator.Annotate(request);

        annotated.Tag.Should().BeNull();
    }

    [Fact]
    public void ClearContext_removes_annotation()
    {
        var annotator = new CaptureContextAnnotator();
        annotator.SetContext(new CaptureContext { Trigger = "click" });
        annotator.ClearContext();
        var annotated = annotator.Annotate(MakeRequest());

        annotated.Tag.Should().BeNull();
    }

    private static CapturedRequest MakeRequest() => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = "GET",
        Url = "https://example.com/api",
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 50,
    };
}
