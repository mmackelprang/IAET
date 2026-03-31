using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class ConfidenceAnnotator
{
    public static string Annotate(
        string diagram,
        ConfidenceLevel confidence,
        int observationCount,
        string source,
        IReadOnlyList<string>? limitations = null)
    {
        var sb = new StringBuilder(diagram);

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"    %% Confidence: {confidence} — {observationCount} observations from {source}");

        if (limitations is not null && limitations.Count > 0)
        {
            foreach (var limitation in limitations)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    %% Limitation: {limitation}");
            }
        }

        return sb.ToString();
    }
}
