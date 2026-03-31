using System.CommandLine;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Export;
using Iaet.Export.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class ExportCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var exportCmd = new Command("export", "Export captured session data to various formats");

        var sessionIdOption = new Option<Guid>("--session-id") { Description = "Session ID", Required = true };
        var outputOption    = new Option<string?>("--output") { Description = "Output file path (default: stdout via -)", DefaultValueFactory = _ => "-" };

        exportCmd.Add(CreateSubcommand("report",  "Generate a Markdown investigation report",   sessionIdOption, outputOption, services,
            ctx => MarkdownReportGenerator.Generate(ctx), "Markdown report"));

        exportCmd.Add(CreateSubcommand("html",    "Generate a self-contained HTML report",       sessionIdOption, outputOption, services,
            ctx => HtmlReportGenerator.Generate(ctx), "HTML report"));

        exportCmd.Add(CreateSubcommand("openapi", "Generate an OpenAPI 3.1 YAML specification", sessionIdOption, outputOption, services,
            ctx => OpenApiGenerator.Generate(ctx), "OpenAPI spec"));

        exportCmd.Add(CreateSubcommand("postman", "Generate a Postman Collection v2.1.0 JSON",  sessionIdOption, outputOption, services,
            ctx => PostmanGenerator.Generate(ctx), "Postman collection"));

        exportCmd.Add(CreateSubcommand("csharp",  "Generate a typed C# HTTP client",            sessionIdOption, outputOption, services,
            ctx => CSharpClientGenerator.Generate(ctx), "C# client"));

        exportCmd.Add(CreateSubcommand("har",     "Generate a HAR 1.2 HTTP archive",            sessionIdOption, outputOption, services,
            ctx => HarGenerator.Generate(ctx), "HAR archive"));

        exportCmd.Add(CreateSubcommand("narrative", "Generate investigation narrative report",
            sessionIdOption, outputOption, services,
            ctx => InvestigationNarrativeGenerator.Generate(ctx), "Investigation narrative"));

        exportCmd.Add(CreateSubcommand("client-prompt", "Generate AI client generation prompt",
            sessionIdOption, outputOption, services,
            ctx => ClientPromptGenerator.Generate(ctx), "Client generation prompt"));

        return exportCmd;
    }

    // ------------------------------------------------------------------

    private static Command CreateSubcommand(
        string name,
        string description,
        Option<Guid> sessionIdOption,
        Option<string?> outputOption,
        IServiceProvider services,
        Func<ExportContext, string> generator,
        string formatName)
    {
        var cmd = new Command(name, description);
        cmd.Add(sessionIdOption);
        cmd.Add(outputOption);

        cmd.SetAction(async (parseResult) =>
        {
            var sessionId  = parseResult.GetRequiredValue(sessionIdOption);
            var outputPath = parseResult.GetValue(outputOption);

            await HandleExport(sessionId, outputPath, services, generator, formatName).ConfigureAwait(false);
        });

        return cmd;
    }

    private static async Task HandleExport(
        Guid sessionId,
        string? outputPath,
        IServiceProvider services,
        Func<ExportContext, string> generator,
        string formatName)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var catalog        = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var streamCatalog  = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();
        var schemaInferrer = scope.ServiceProvider.GetRequiredService<ISchemaInferrer>();

        var ctx    = await ExportContext.LoadAsync(sessionId, catalog, streamCatalog, schemaInferrer).ConfigureAwait(false);
        var output = generator(ctx);

        if (!string.IsNullOrEmpty(outputPath) && outputPath != "-")
        {
            await File.WriteAllTextAsync(outputPath, output).ConfigureAwait(false);
            Console.WriteLine($"{formatName} written to {outputPath}");
        }
        else
        {
            Console.Write(output);
        }
    }
}
