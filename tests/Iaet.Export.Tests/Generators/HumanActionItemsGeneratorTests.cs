using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class HumanActionItemsGeneratorTests
{
    [Fact]
    public void Generate_lists_action_items()
    {
        var items = new List<HumanActionRequest>
        {
            new() { Action = "Verify PRACK sequence manually", Reason = "Only 1 observation" },
            new() { Action = "Test SMS API with real message", Reason = "Needs live account" },
        };

        var markdown = HumanActionItemsGenerator.Generate("google-voice", items);

        markdown.Should().Contain("# Human Action Items");
        markdown.Should().Contain("Verify PRACK sequence manually");
        markdown.Should().Contain("Test SMS API with real message");
    }

    [Fact]
    public void Generate_shows_urgency()
    {
        var items = new List<HumanActionRequest>
        {
            new() { Action = "Re-authenticate", Reason = "Cookie expired", Urgency = "high" },
        };

        var markdown = HumanActionItemsGenerator.Generate("proj", items);

        markdown.Should().Contain("high");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var markdown = HumanActionItemsGenerator.Generate("proj", []);

        markdown.Should().Contain("No action items");
    }
}
