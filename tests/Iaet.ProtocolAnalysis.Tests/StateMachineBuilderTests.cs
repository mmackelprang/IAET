using FluentAssertions;
using Iaet.Core.Models;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class StateMachineBuilderTests
{
    [Fact]
    public void Build_from_ordered_message_types()
    {
        var messageSequence = new[] { "connection_init", "connection_ack", "subscribe", "data", "complete" };

        var sm = StateMachineBuilder.Build("GraphQL-WS", messageSequence);

        sm.Name.Should().Be("GraphQL-WS");
        sm.InitialState.Should().Be("connection_init");
        sm.States.Should().Contain(["connection_init", "connection_ack", "subscribe", "data", "complete"]);
        sm.Transitions.Should().HaveCount(4);
        sm.Transitions[0].From.Should().Be("connection_init");
        sm.Transitions[0].To.Should().Be("connection_ack");
    }

    [Fact]
    public void Build_deduplicates_transitions()
    {
        var messages = new[] { "ping", "pong", "ping", "pong", "data" };

        var sm = StateMachineBuilder.Build("WS", messages);

        sm.Transitions.Should().HaveCount(3); // ping→pong, pong→ping, pong→data (deduped)
    }

    [Fact]
    public void Build_handles_empty_sequence()
    {
        var sm = StateMachineBuilder.Build("Empty", []);

        sm.States.Should().BeEmpty();
        sm.Transitions.Should().BeEmpty();
    }

    [Fact]
    public void Build_handles_single_message()
    {
        var sm = StateMachineBuilder.Build("Single", ["init"]);

        sm.States.Should().Contain("init");
        sm.Transitions.Should().BeEmpty();
    }
}
