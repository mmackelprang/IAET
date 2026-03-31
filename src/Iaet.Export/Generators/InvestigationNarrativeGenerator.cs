using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

public static class InvestigationNarrativeGenerator
{
    public static string Generate(ExportContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sb = new StringBuilder();

        sb.AppendLine("# Investigation Report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Target:** {ctx.Session.TargetApplication}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Session:** {ctx.Session.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Started:** {ctx.Session.StartedAt:u}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Requests:** {ctx.Requests.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Endpoints:** {ctx.EndpointGroups.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Streams:** {ctx.Streams.Count}");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();

        sb.AppendLine("## Endpoints Discovered");
        sb.AppendLine();

        if (ctx.EndpointGroups.Count == 0)
        {
            sb.AppendLine("No endpoints discovered.");
        }
        else
        {
            sb.AppendLine("| Endpoint | Observations | First Seen |");
            sb.AppendLine("|----------|-------------|------------|");
            foreach (var g in ctx.EndpointGroups)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| `{g.Signature.Normalized}` | {g.ObservationCount} | {g.FirstSeen:u} |");
            }
        }

        sb.AppendLine();

        if (ctx.Streams.Count > 0)
        {
            sb.AppendLine("## Streams Captured");
            sb.AppendLine();
            sb.AppendLine("| Protocol | URL | Started |");
            sb.AppendLine("|----------|-----|---------|");
            foreach (var s in ctx.Streams)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {s.Protocol} | `{s.Url}` | {s.StartedAt:u} |");
            }
            sb.AppendLine();
        }

        if (ctx.SchemasByEndpoint.Count > 0)
        {
            sb.AppendLine("## Schema Inference Results");
            sb.AppendLine();
            foreach (var (endpoint, schema) in ctx.SchemasByEndpoint)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"### `{endpoint}`");
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(schema.CSharpRecord);
                sb.AppendLine("```");
                sb.AppendLine();

                if (schema.Warnings.Count > 0)
                {
                    foreach (var w in schema.Warnings)
                        sb.AppendLine(CultureInfo.InvariantCulture, $"> Warning: {w}");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
