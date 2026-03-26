using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

/// <summary>
/// Generates a Markdown API investigation report from an <see cref="ExportContext"/>.
/// </summary>
public static class MarkdownReportGenerator
{
    /// <summary>Generates the Markdown report string.</summary>
    public static string Generate(ExportContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sb = new StringBuilder();

        AppendSessionHeader(sb, ctx);
        AppendEndpointCatalog(sb, ctx);
        AppendEndpointDetails(sb, ctx);
        AppendDataStreams(sb, ctx);
        AppendGenerationInfo(sb);

        return sb.ToString();
    }

    // ------------------------------------------------------------------

    private static void AppendSessionHeader(StringBuilder sb, ExportContext ctx)
    {
        sb.AppendLine("# API Investigation Report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Target Application:** {ctx.Session.TargetApplication}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Session:** {ctx.Session.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Date:** {ctx.Session.StartedAt:yyyy-MM-dd}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Requests Captured:** {ctx.Session.CapturedRequestCount}");
        sb.AppendLine();
    }

    private static void AppendEndpointCatalog(StringBuilder sb, ExportContext ctx)
    {
        sb.AppendLine("## Endpoint Catalog");
        sb.AppendLine();
        sb.AppendLine("| Method | Path | Observations | First Seen | Last Seen |");
        sb.AppendLine("|--------|------|-------------|------------|-----------|");

        foreach (var group in ctx.EndpointGroups)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {group.Signature.Method} " +
                $"| {group.Signature.NormalizedPath} " +
                $"| {group.ObservationCount} " +
                $"| {group.FirstSeen:yyyy-MM-dd HH:mm:ss} UTC " +
                $"| {group.LastSeen:yyyy-MM-dd HH:mm:ss} UTC |");
        }

        sb.AppendLine();
    }

    private static void AppendEndpointDetails(StringBuilder sb, ExportContext ctx)
    {
        sb.AppendLine("## Endpoint Details");
        sb.AppendLine();

        foreach (var group in ctx.EndpointGroups)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {group.Signature.Method} {group.Signature.NormalizedPath}");
            sb.AppendLine();

            // Example request / response — first captured request matching this endpoint
            var match = FindFirstRequest(ctx, group);
            if (match is not null)
            {
                AppendExampleRequest(sb, match);
                AppendExampleResponse(sb, match);
            }

            // Inferred C# record
            if (ctx.SchemasByEndpoint.TryGetValue(group.Signature.Normalized, out var schema))
            {
                sb.AppendLine("#### Inferred C# Record");
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(schema.CSharpRecord);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }
    }

    private static CapturedRequest? FindFirstRequest(ExportContext ctx, EndpointGroup group)
    {
        foreach (var req in ctx.Requests)
        {
            var sig = EndpointSignature.FromRequest(req.HttpMethod, new Uri(req.Url).AbsolutePath);
            if (sig.Normalized == group.Signature.Normalized)
                return req;
        }

        return null;
    }

    private static void AppendExampleRequest(StringBuilder sb, CapturedRequest req)
    {
        sb.AppendLine("#### Example Request");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"> `{req.HttpMethod} {req.Url}`");
        sb.AppendLine();

        if (req.RequestHeaders.Count > 0)
        {
            sb.AppendLine("**Headers:**");
            sb.AppendLine();
            foreach (var (key, value) in req.RequestHeaders)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{key}: {RedactHeaderValue(key, value)}`");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(req.RequestBody))
        {
            sb.AppendLine("**Body:**");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(req.RequestBody);
            sb.AppendLine("```");
            sb.AppendLine();
        }
    }

    private static void AppendExampleResponse(StringBuilder sb, CapturedRequest req)
    {
        sb.AppendLine("#### Example Response");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"> HTTP {req.ResponseStatus}");
        sb.AppendLine();

        if (req.ResponseHeaders.Count > 0)
        {
            sb.AppendLine("**Headers:**");
            sb.AppendLine();
            foreach (var (key, value) in req.ResponseHeaders)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{key}: {RedactHeaderValue(key, value)}`");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(req.ResponseBody))
        {
            sb.AppendLine("**Body:**");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(req.ResponseBody);
            sb.AppendLine("```");
            sb.AppendLine();
        }
    }

    private static void AppendDataStreams(StringBuilder sb, ExportContext ctx)
    {
        if (ctx.Streams.Count == 0)
            return;

        sb.AppendLine("## Data Streams");
        sb.AppendLine();
        sb.AppendLine("| Protocol | URL | Metadata |");
        sb.AppendLine("|----------|-----|----------|");

        foreach (var stream in ctx.Streams)
        {
            var metaSummary = stream.Metadata.Properties.Count > 0
                ? string.Join(", ", stream.Metadata.Properties.Select(kv =>
                    string.Create(CultureInfo.InvariantCulture, $"{kv.Key}={kv.Value}")))
                : "(none)";

            sb.AppendLine(CultureInfo.InvariantCulture, $"| {stream.Protocol} | {stream.Url} | {metaSummary} |");
        }

        sb.AppendLine();
    }

    private static void AppendGenerationInfo(StringBuilder sb)
    {
        sb.AppendLine("## Generation Info");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Generated at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
    }

    /// <summary>
    /// Returns <c>&lt;REDACTED&gt;</c> when the value already contains that sentinel,
    /// or when it looks like a credential (Bearer token, session cookie, etc.).
    /// All other values are returned unchanged.
    /// </summary>
    private static string RedactHeaderValue(string key, string value)
    {
        if (value.Contains("<REDACTED>", StringComparison.OrdinalIgnoreCase))
            return "<REDACTED>";

        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "<REDACTED>";

        if (value.Contains("session=", StringComparison.OrdinalIgnoreCase))
            return "<REDACTED>";

        // Treat Authorization / Cookie headers as always sensitive
        if (key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
        {
            return "<REDACTED>";
        }

        return value;
    }
}
