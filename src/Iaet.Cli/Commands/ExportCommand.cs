using System.CommandLine;
using System.Globalization;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Export;
using Iaet.Export.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class ExportCommand
{
    // Maps subcommand name to default file extension for --project output.
    private static readonly Dictionary<string, string> FormatExtensions = new(StringComparer.Ordinal)
    {
        ["report"] = ".md",
        ["html"] = ".html",
        ["openapi"] = ".yaml",
        ["postman"] = ".postman.json",
        ["csharp"] = ".cs",
        ["har"] = ".har",
        ["narrative"] = ".narrative.md",
        ["client-prompt"] = ".prompt.md",
    };

    internal static Command Create(IServiceProvider services)
    {
        var exportCmd = new Command("export", "Export captured session data to various formats");

        var sessionIdOption = new Option<Guid>("--session-id") { Description = "Session ID", Required = true };
        var outputOption    = new Option<string?>("--output") { Description = "Output file path (default: stdout via -)", DefaultValueFactory = _ => "-" };
        var projectOption   = new Option<string?>("--project") { Description = "Write output to the project's output directory" };

        exportCmd.Add(CreateSubcommand("report",  "Generate a Markdown investigation report",   sessionIdOption, outputOption, projectOption, services,
            ctx => MarkdownReportGenerator.Generate(ctx), "Markdown report"));

        exportCmd.Add(CreateSubcommand("html",    "Generate a self-contained HTML report",       sessionIdOption, outputOption, projectOption, services,
            ctx => HtmlReportGenerator.Generate(ctx), "HTML report"));

        exportCmd.Add(CreateSubcommand("openapi", "Generate an OpenAPI 3.1 YAML specification", sessionIdOption, outputOption, projectOption, services,
            ctx => OpenApiGenerator.Generate(ctx), "OpenAPI spec"));

        exportCmd.Add(CreateSubcommand("postman", "Generate a Postman Collection v2.1.0 JSON",  sessionIdOption, outputOption, projectOption, services,
            ctx => PostmanGenerator.Generate(ctx), "Postman collection"));

        exportCmd.Add(CreateSubcommand("csharp",  "Generate a typed C# HTTP client",            sessionIdOption, outputOption, projectOption, services,
            ctx => CSharpClientGenerator.Generate(ctx), "C# client"));

        exportCmd.Add(CreateSubcommand("har",     "Generate a HAR 1.2 HTTP archive",            sessionIdOption, outputOption, projectOption, services,
            ctx => HarGenerator.Generate(ctx), "HAR archive"));

        exportCmd.Add(CreateSubcommand("narrative", "Generate investigation narrative report",
            sessionIdOption, outputOption, projectOption, services,
            ctx => InvestigationNarrativeGenerator.Generate(ctx), "Investigation narrative"));

        exportCmd.Add(CreateSubcommand("client-prompt", "Generate AI client generation prompt",
            sessionIdOption, outputOption, projectOption, services,
            ctx => ClientPromptGenerator.Generate(ctx), "Client generation prompt"));

        return exportCmd;
    }

    // ------------------------------------------------------------------

    private static Command CreateSubcommand(
        string name,
        string description,
        Option<Guid> sessionIdOption,
        Option<string?> outputOption,
        Option<string?> projectOption,
        IServiceProvider services,
        Func<ExportContext, string> generator,
        string formatName)
    {
        var cmd = new Command(name, description);
        cmd.Add(sessionIdOption);
        cmd.Add(outputOption);
        cmd.Add(projectOption);

        var subcommandName = name;
        cmd.SetAction(async (parseResult) =>
        {
            var sessionId   = parseResult.GetRequiredValue(sessionIdOption);
            var outputPath  = parseResult.GetValue(outputOption);
            var projectName = parseResult.GetValue(projectOption);

            await HandleExport(sessionId, outputPath, projectName, subcommandName, services, generator, formatName).ConfigureAwait(false);
        });

        return cmd;
    }

    private static async Task HandleExport(
        Guid sessionId,
        string? outputPath,
        string? projectName,
        string subcommandName,
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

        // When --project is specified, compute default output path in the project's output directory
        if (projectName is not null && (string.IsNullOrEmpty(outputPath) || outputPath == "-"))
        {
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var projectConfig = await projectStore.LoadAsync(projectName).ConfigureAwait(false);
            if (projectConfig is null)
            {
                await Console.Error.WriteLineAsync($"Project '{projectName}' not found.").ConfigureAwait(false);
                return;
            }

            var projectDir = projectStore.GetProjectDirectory(projectName);
            var projectOutputDir = Path.Combine(projectDir, "output");
            Directory.CreateDirectory(projectOutputDir);

            var extension = FormatExtensions.GetValueOrDefault(subcommandName, ".txt");
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            outputPath = Path.Combine(projectOutputDir, $"{timestamp}-{subcommandName}{extension}");
        }

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
