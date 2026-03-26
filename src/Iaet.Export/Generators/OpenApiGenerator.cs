using System.Globalization;
using System.Text;

namespace Iaet.Export.Generators;

/// <summary>
/// Generates an OpenAPI 3.1 YAML document from an <see cref="ExportContext"/>.
/// </summary>
public static class OpenApiGenerator
{
    /// <summary>Generates the OpenAPI 3.1 YAML string.</summary>
    public static string Generate(ExportContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sb = new StringBuilder();

        sb.AppendLine("openapi: '3.1.0'");
        sb.AppendLine("info:");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  title: '{ctx.Session.TargetApplication} API'");
        sb.AppendLine("  version: '1.0.0'");

        AppendServers(sb, ctx);
        AppendPaths(sb, ctx);
        AppendComponents(sb, ctx);

        return sb.ToString();
    }

    // ------------------------------------------------------------------

    private static void AppendServers(StringBuilder sb, ExportContext ctx)
    {
        sb.AppendLine("servers:");

        var baseUrl = ExtractBaseUrl(ctx);
        sb.AppendLine(CultureInfo.InvariantCulture, $"  - url: '{baseUrl}'");
    }

    private static string ExtractBaseUrl(ExportContext ctx)
    {
        if (ctx.Requests.Count == 0)
            return "https://localhost";

        var uri = new Uri(ctx.Requests[0].Url);
        return string.Create(CultureInfo.InvariantCulture, $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? string.Empty : $":{uri.Port}")}");
    }

    private static void AppendPaths(StringBuilder sb, ExportContext ctx)
    {
        sb.AppendLine("paths:");

        foreach (var pathGroup in ctx.EndpointGroups.GroupBy(g => g.Signature.NormalizedPath))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  '{pathGroup.Key}':");

            foreach (var group in pathGroup)
            {
                var method = HttpMethodToYamlKey(group.Signature.Method);
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {method}:");
                sb.AppendLine("      summary: ''");
                sb.AppendLine("      responses:");
                sb.AppendLine("        '200':");
                sb.AppendLine("          description: OK");

                if (ctx.SchemasByEndpoint.TryGetValue(group.Signature.Normalized, out _))
                {
                    var schemaRef = BuildSchemaRefName(group.Signature.Normalized);
                    sb.AppendLine("          content:");
                    sb.AppendLine("            application/json:");
                    sb.AppendLine("              schema:");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"                $ref: '#/components/schemas/{schemaRef}'");
                }
            }
        }
    }

    private static void AppendComponents(StringBuilder sb, ExportContext ctx)
    {
        if (ctx.SchemasByEndpoint.Count == 0)
            return;

        sb.AppendLine("components:");
        sb.AppendLine("  schemas:");

        foreach (var (key, schema) in ctx.SchemasByEndpoint)
        {
            var refName = BuildSchemaRefName(key);
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {refName}:");

            // Indent the OpenAPI fragment by 6 spaces
            foreach (var line in schema.OpenApiFragment.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length == 0)
                    continue;
                sb.AppendLine(CultureInfo.InvariantCulture, $"      {trimmed}");
            }
        }
    }

    /// <summary>
    /// Returns the lowercase YAML key for a given HTTP method (e.g. "GET" → "get").
    /// Uses a static map to avoid CA1308.
    /// </summary>
    private static string HttpMethodToYamlKey(string method) => method switch
    {
        "GET"     => "get",
        "POST"    => "post",
        "PUT"     => "put",
        "PATCH"   => "patch",
        "DELETE"  => "delete",
        "HEAD"    => "head",
        "OPTIONS" => "options",
        "TRACE"   => "trace",
        _         => method,
    };

    /// <summary>
    /// Converts a normalized key like <c>GET /api/users/{id}</c> to a safe YAML schema
    /// reference name like <c>GetApiUsersId</c>.
    /// </summary>
    private static string BuildSchemaRefName(string normalized)
    {
        var parts = normalized.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            var clean = part.Replace("{", string.Empty, StringComparison.Ordinal)
                            .Replace("}", string.Empty, StringComparison.Ordinal);
            if (clean.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(clean[0]));
                sb.Append(clean[1..]);
            }
        }
        return sb.ToString();
    }
}
