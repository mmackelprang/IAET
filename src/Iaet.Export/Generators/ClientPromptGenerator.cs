// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

/// <summary>
/// Generates a structured prompt optimized for AI-assisted API client code generation.
/// The output can be fed to Claude, GPT, or other AI coding assistants to produce
/// a full-fledged typed API client in C#, Python, TypeScript, or other languages.
/// </summary>
public static class ClientPromptGenerator
{
    public static string Generate(ExportContext ctx, string language = "C#")
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sb = new StringBuilder();

        sb.AppendLine("# API Client Generation Request");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Generate a complete, production-ready **{language}** API client for the following API.");
        sb.AppendLine();

        // Target info
        sb.AppendLine("## Target Application");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Name:** {ctx.Session.TargetApplication}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Session:** {ctx.Session.Name}");

        // Extract base URLs
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in ctx.Requests)
        {
            if (Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
                hosts.Add($"{uri.Scheme}://{uri.Host}");
        }
        if (hosts.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Base URLs:** {string.Join(", ", hosts)}");
        }
        sb.AppendLine();

        // Authentication
        sb.AppendLine("## Authentication");
        sb.AppendLine();
        var hasAuth = ctx.Requests.Any(r => r.RequestHeaders.ContainsKey("Authorization")
            || r.RequestHeaders.ContainsKey("X-Goog-Api-Key"));
        if (hasAuth)
        {
            sb.AppendLine("This API uses the following authentication mechanisms:");
            var authHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var req in ctx.Requests)
            {
                foreach (var key in req.RequestHeaders.Keys)
                {
                    if (key.Contains("auth", StringComparison.OrdinalIgnoreCase)
                        || key.Contains("api-key", StringComparison.OrdinalIgnoreCase)
                        || key.Contains("cookie", StringComparison.OrdinalIgnoreCase))
                    {
                        authHeaders.Add(key);
                    }
                }
            }
            foreach (var h in authHeaders.OrderBy(h => h, StringComparer.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{h}` header");
            }
            sb.AppendLine();
            sb.AppendLine("The client should accept these credentials via constructor parameters or configuration.");
        }
        else
        {
            sb.AppendLine("No authentication headers detected. The API may be public or use cookie-based auth.");
        }
        sb.AppendLine();

        // Endpoints
        sb.AppendLine("## API Endpoints");
        sb.AppendLine();
        sb.AppendLine("Generate a typed method for each endpoint:");
        sb.AppendLine();

        foreach (var group in ctx.EndpointGroups)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### `{group.Signature.Normalized}`");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Observed {group.ObservationCount} time(s)");

            // Find a sample request
            var sample = ctx.Requests.FirstOrDefault(r =>
            {
                if (!Uri.TryCreate(r.Url, UriKind.Absolute, out var uri))
                    return false;
                var sig = EndpointSignature.FromRequest(r.HttpMethod, uri.AbsolutePath);
                return sig.Normalized == group.Signature.Normalized;
            });

            if (sample is not null)
            {
                if (sample.RequestBody is not null)
                {
                    var bodyPreview = sample.RequestBody.Length > 500
                        ? sample.RequestBody[..500] + "..."
                        : sample.RequestBody;
                    sb.AppendLine("- **Request body sample:**");
                    sb.AppendLine("```json");
                    sb.AppendLine(bodyPreview);
                    sb.AppendLine("```");
                }

                if (sample.ResponseBody is not null)
                {
                    var bodyPreview = sample.ResponseBody.Length > 500
                        ? sample.ResponseBody[..500] + "..."
                        : sample.ResponseBody;
                    sb.AppendLine(CultureInfo.InvariantCulture, $"- **Response status:** {sample.ResponseStatus}");
                    sb.AppendLine("- **Response body sample:**");
                    sb.AppendLine("```json");
                    sb.AppendLine(bodyPreview);
                    sb.AppendLine("```");
                }
            }

            // Include schema if available
            if (ctx.SchemasByEndpoint.TryGetValue(group.Signature.Normalized, out var schema))
            {
                if (!string.IsNullOrWhiteSpace(schema.CSharpRecord))
                {
                    sb.AppendLine("- **Inferred response type:**");
                    sb.AppendLine("```csharp");
                    sb.AppendLine(schema.CSharpRecord);
                    sb.AppendLine("```");
                }
            }

            sb.AppendLine();
        }

        // Streams
        if (ctx.Streams.Count > 0)
        {
            sb.AppendLine("## Streaming Protocols");
            sb.AppendLine();
            sb.AppendLine("The API also uses these real-time protocols. Include support for them in the client:");
            sb.AppendLine();
            foreach (var stream in ctx.Streams)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **{stream.Protocol}** at `{stream.Url}`");
                foreach (var (key, value) in stream.Metadata.Properties)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  - {key}: {value}");
                }
            }
            sb.AppendLine();
        }

        // Requirements
        sb.AppendLine("## Client Requirements");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Language: **{language}**");
        sb.AppendLine("- Use async/await for all API calls");
        sb.AppendLine("- Include typed request/response models for all endpoints");
        sb.AppendLine("- Support configurable base URL");
        sb.AppendLine("- Support configurable authentication (API key, OAuth token, cookies)");
        sb.AppendLine("- Include retry logic with exponential backoff");
        sb.AppendLine("- Include proper error handling with typed exceptions");
        sb.AppendLine("- Include XML doc comments / docstrings for all public members");
        sb.AppendLine("- Follow idiomatic patterns for the target language");
        sb.AppendLine();

        sb.AppendLine("Generate the complete client code now.");

        return sb.ToString();
    }
}
