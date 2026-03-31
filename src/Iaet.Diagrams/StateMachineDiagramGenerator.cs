using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class StateMachineDiagramGenerator
{
    public static string Generate(StateMachineModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    %% {model.Name}");

        if (!string.IsNullOrEmpty(model.InitialState))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    [*] --> {model.InitialState}");
        }

        foreach (var transition in model.Transitions)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {transition.From} --> {transition.To} : {transition.Trigger}");
        }

        return sb.ToString();
    }
}
