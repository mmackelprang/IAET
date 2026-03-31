using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

public static class CoverageReportGenerator
{
    public static string Generate(ExportContext ctx, IReadOnlyList<ExtractedUrl>? knownUrls = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sb = new StringBuilder();
        sb.AppendLine("# Coverage Report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Target:** {ctx.Session.TargetApplication}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Session:** {ctx.Session.Name}");
        sb.AppendLine();

        var observedSignatures = new HashSet<string>(
            ctx.EndpointGroups.Select(g => g.Signature.Normalized),
            StringComparer.OrdinalIgnoreCase);

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Observed endpoints:** {ctx.EndpointGroups.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Total requests:** {ctx.Requests.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Streams captured:** {ctx.Streams.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Schemas inferred:** {ctx.SchemasByEndpoint.Count}");

        if (knownUrls is not null && knownUrls.Count > 0)
        {
            var matched = knownUrls.Count(k => observedSignatures.Any(s =>
                s.Contains(k.Url, StringComparison.OrdinalIgnoreCase)));
            var pct = knownUrls.Count > 0 ? (matched * 100) / knownUrls.Count : 0;

            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Known endpoints:** {knownUrls.Count}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Coverage:** {pct}% ({matched}/{knownUrls.Count})");
        }

        sb.AppendLine();

        sb.AppendLine("## Observed Endpoints");
        sb.AppendLine();

        if (ctx.EndpointGroups.Count == 0)
        {
            sb.AppendLine("0 endpoints observed.");
        }
        else
        {
            sb.AppendLine("| Endpoint | Status | Observations | Has Schema |");
            sb.AppendLine("|----------|--------|-------------|------------|");
            foreach (var g in ctx.EndpointGroups)
            {
                var hasSchema = ctx.SchemasByEndpoint.ContainsKey(g.Signature.Normalized) ? "Yes" : "No";
                sb.AppendLine(CultureInfo.InvariantCulture, $"| `{g.Signature.Normalized}` | Observed | {g.ObservationCount} | {hasSchema} |");
            }
        }

        if (knownUrls is not null)
        {
            var unobserved = knownUrls.Where(k => !observedSignatures.Any(s =>
                s.Contains(k.Url, StringComparison.OrdinalIgnoreCase))).ToList();

            if (unobserved.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Not observed (from JS analysis)");
                sb.AppendLine();
                sb.AppendLine("| URL | Confidence | Source |");
                sb.AppendLine("|-----|-----------|--------|");
                foreach (var u in unobserved)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"| `{u.Url}` | {u.Confidence} | {u.SourceFile ?? "unknown"} |");
                }
            }
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
