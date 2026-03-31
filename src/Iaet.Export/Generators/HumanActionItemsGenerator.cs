using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

public static class HumanActionItemsGenerator
{
    public static string Generate(string projectName, IReadOnlyList<HumanActionRequest> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var sb = new StringBuilder();
        sb.AppendLine("# Human Action Items");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Project:** {projectName}");
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("No action items remaining.");
            return sb.ToString();
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"{items.Count} item(s) requiring human attention:");
        sb.AppendLine();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var urgencyTag = item.Urgency != "normal" ? $" [{item.Urgency}]" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {i + 1}. {item.Action}{urgencyTag}");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Reason:** {item.Reason}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
