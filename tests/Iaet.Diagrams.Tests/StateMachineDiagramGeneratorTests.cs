using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class StateMachineDiagramGeneratorTests
{
    [Fact]
    public void Generate_creates_state_diagram()
    {
        var sm = new StateMachineModel
        {
            Name = "WebRTC",
            States = ["new", "connecting", "connected", "disconnected"],
            Transitions =
            [
                new StateTransition { From = "new", To = "connecting", Trigger = "createOffer" },
                new StateTransition { From = "connecting", To = "connected", Trigger = "iceComplete" },
                new StateTransition { From = "connected", To = "disconnected", Trigger = "close" },
            ],
            InitialState = "new",
        };

        var mermaid = StateMachineDiagramGenerator.Generate(sm);

        mermaid.Should().StartWith("stateDiagram-v2");
        mermaid.Should().Contain("[*] --> new");
        mermaid.Should().Contain("new --> connecting : createOffer");
        mermaid.Should().Contain("connecting --> connected : iceComplete");
    }

    [Fact]
    public void Generate_handles_empty_state_machine()
    {
        var sm = new StateMachineModel
        {
            Name = "Empty", States = [], Transitions = [], InitialState = "",
        };

        var mermaid = StateMachineDiagramGenerator.Generate(sm);

        mermaid.Should().Contain("stateDiagram-v2");
    }
}
