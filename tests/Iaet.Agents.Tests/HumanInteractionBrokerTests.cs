using FluentAssertions;
using Iaet.Agents;
using Iaet.Core.Models;

namespace Iaet.Agents.Tests;

public sealed class HumanInteractionBrokerTests
{
    [Fact]
    public async Task RequestAction_writes_to_console_and_waits()
    {
        var input = new StringReader("done\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);
        var request = new HumanActionRequest { Action = "Please log in", Reason = "Auth required" };
        await broker.RequestActionAsync(request);
        output.ToString().Should().Contain("Please log in");
        output.ToString().Should().Contain("Auth required");
    }

    [Fact]
    public async Task RequestConfirmation_returns_true_for_y()
    {
        var input = new StringReader("y\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);
        var result = await broker.RequestConfirmationAsync("Proceed?");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestConfirmation_returns_false_for_n()
    {
        var input = new StringReader("n\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);
        var result = await broker.RequestConfirmationAsync("Proceed?");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestConfirmation_returns_true_for_empty_default_yes()
    {
        var input = new StringReader("\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);
        var result = await broker.RequestConfirmationAsync("Proceed?", defaultYes: true);
        result.Should().BeTrue();
    }
}
