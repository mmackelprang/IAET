using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class DependencyGraphDiagramGenerator
{
    public static string Generate(string title, IReadOnlyList<RequestDependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    %% {title}");

        if (dependencies.Count == 0)
            return sb.ToString();

        var nodeIndex = 0;
        var nodeMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var dep in dependencies)
        {
            var fromId = GetOrCreateNode(nodeMap, dep.From, ref nodeIndex);
            var toId = GetOrCreateNode(nodeMap, dep.To, ref nodeIndex);
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {fromId} -->|\"{dep.Reason}\"| {toId}");
        }

        foreach (var (label, id) in nodeMap)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {id}[\"{label}\"]");
        }

        return sb.ToString();
    }

    public static string GenerateFromAuthChains(string title, IReadOnlyList<AuthChain> chains)
    {
        ArgumentNullException.ThrowIfNull(chains);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    %% {title}");

        var nodeIndex = 0;
        var nodeMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var chain in chains)
        {
            for (var i = 0; i < chain.Steps.Count - 1; i++)
            {
                var fromStep = chain.Steps[i];
                var toStep = chain.Steps[i + 1];

                var fromId = GetOrCreateNode(nodeMap, fromStep.Endpoint, ref nodeIndex);
                var toId = GetOrCreateNode(nodeMap, toStep.Endpoint, ref nodeIndex);
                var label = fromStep.Provides ?? toStep.Consumes ?? "depends";

                sb.AppendLine(CultureInfo.InvariantCulture, $"    {fromId} -->|\"{label}\"| {toId}");
            }
        }

        foreach (var (label, id) in nodeMap)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {id}[\"{label}\"]");
        }

        return sb.ToString();
    }

    private static string GetOrCreateNode(Dictionary<string, string> map, string label, ref int index)
    {
        if (!map.TryGetValue(label, out var id))
        {
            id = $"N{index++}";
            map[label] = id;
        }
        return id;
    }
}
