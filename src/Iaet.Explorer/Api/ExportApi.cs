using Iaet.Core.Abstractions;
using Iaet.Export;
using Iaet.Export.Generators;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.Explorer.Api;

/// <summary>
/// Minimal API endpoints for exporting session data as file downloads.
/// </summary>
internal static class ExportApi
{
    private static readonly IReadOnlyDictionary<string, (string ContentType, string Extension, Func<ExportContext, string> Generator)> Formats =
        new Dictionary<string, (string, string, Func<ExportContext, string>)>(StringComparer.OrdinalIgnoreCase)
        {
            ["report"]  = ("text/markdown",        "md",   MarkdownReportGenerator.Generate),
            ["html"]    = ("text/html",             "html", HtmlReportGenerator.Generate),
            ["openapi"] = ("application/yaml",      "yaml", OpenApiGenerator.Generate),
            ["postman"] = ("application/json",      "json", PostmanGenerator.Generate),
            ["csharp"]  = ("text/x-csharp",         "cs",   CSharpClientGenerator.Generate),
            ["har"]     = ("application/json",      "json", HarGenerator.Generate),
        };

    internal static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions/{id:guid}/export/{format}",
            async (Guid id, string format, IEndpointCatalog catalog, IStreamCatalog streamCatalog, ISchemaInferrer schemaInferrer, CancellationToken ct) =>
            {
                if (!Formats.TryGetValue(format, out var entry))
                    return Results.BadRequest(new { message = $"Unknown format '{format}'. Supported: {string.Join(", ", Formats.Keys)}." });

                ExportContext ctx;
                try
                {
                    ctx = await ExportContext.LoadAsync(id, catalog, streamCatalog, schemaInferrer, ct).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }

                var output = entry.Generator(ctx);
                var fileName = $"{ctx.Session.Name}-{format}.{entry.Extension}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(output);
                return Results.File(bytes, entry.ContentType, fileName);
            });
    }
}
