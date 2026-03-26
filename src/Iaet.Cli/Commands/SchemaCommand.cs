using System.CommandLine;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class SchemaCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var schemaCmd = new Command("schema", "Infer JSON schemas from captured response bodies");

        var sessionIdOption = new Option<Guid>("--session-id") { Description = "Session ID", Required = true };
        var endpointOption  = new Option<string>("--endpoint") { Description = "Normalized endpoint signature (e.g. 'GET /api/users')", Required = true };

        // ── infer ──────────────────────────────────────────────────────────────
        var inferCmd = new Command("infer", "Infer and print all schema formats for an endpoint");
        inferCmd.Add(sessionIdOption);
        inferCmd.Add(endpointOption);

        inferCmd.SetAction(async (parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionIdOption);
            var endpoint  = parseResult.GetRequiredValue(endpointOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);

            var catalog  = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var inferrer = scope.ServiceProvider.GetRequiredService<ISchemaInferrer>();

            var bodies = await catalog.GetResponseBodiesAsync(sessionId, endpoint).ConfigureAwait(false);

            if (bodies.Count == 0)
            {
                Console.WriteLine("No response bodies found for the specified session/endpoint.");
                return;
            }

            var result = await inferrer.InferAsync(bodies).ConfigureAwait(false);

            Console.WriteLine("=== JSON Schema ===");
            Console.WriteLine(result.JsonSchema);

            Console.WriteLine();
            Console.WriteLine("=== C# Record ===");
            Console.WriteLine(result.CSharpRecord);

            Console.WriteLine();
            Console.WriteLine("=== OpenAPI Fragment ===");
            Console.WriteLine(result.OpenApiFragment);

            if (result.Warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== Warnings ===");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
            }
        });

        // ── show ───────────────────────────────────────────────────────────────
        var formatOption = new Option<string>("--format")
        {
            Description = "Output format: json, csharp, or openapi",
            Required    = true,
        };

        var showCmd = new Command("show", "Show a single schema format for an endpoint");
        showCmd.Add(sessionIdOption);
        showCmd.Add(endpointOption);
        showCmd.Add(formatOption);

        showCmd.SetAction(async (parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionIdOption);
            var endpoint  = parseResult.GetRequiredValue(endpointOption);
            var format    = parseResult.GetRequiredValue(formatOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);

            var catalog  = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
            var inferrer = scope.ServiceProvider.GetRequiredService<ISchemaInferrer>();

            var bodies = await catalog.GetResponseBodiesAsync(sessionId, endpoint).ConfigureAwait(false);

            if (bodies.Count == 0)
            {
                Console.WriteLine("No response bodies found for the specified session/endpoint.");
                return;
            }

            var result = await inferrer.InferAsync(bodies).ConfigureAwait(false);

            var output = format.ToUpperInvariant() switch
            {
                "JSON"    or "JSON-SCHEMA" => result.JsonSchema,
                "CSHARP"  or "CS"          => result.CSharpRecord,
                "OPENAPI" or "OAS"         => result.OpenApiFragment,
                _                          => null,
            };

            if (output is null)
            {
                await Console.Error.WriteLineAsync($"Unknown format '{format}'. Valid values: json, csharp, openapi").ConfigureAwait(false);
                return;
            }

            Console.WriteLine(output);
        });

        schemaCmd.Add(inferCmd);
        schemaCmd.Add(showCmd);
        return schemaCmd;
    }
}
