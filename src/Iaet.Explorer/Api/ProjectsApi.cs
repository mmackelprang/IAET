using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Iaet.Explorer.Api;

/// <summary>
/// Minimal API endpoints for project browsing and management.
/// </summary>
internal static class ProjectsApi
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        // List all projects (summary view)
        app.MapGet("/api/projects", async (IProjectStore store) =>
        {
            var projects = await store.ListAsync().ConfigureAwait(false);
            return Results.Ok(projects.Select(p => new
            {
                p.Name,
                p.DisplayName,
                p.Status,
                p.TargetType,
                p.CurrentRound,
                p.LastActivityAt,
                Url = p.EntryPoints.Count > 0 ? p.EntryPoints[0].Url : null,
            }));
        });

        // Get single project details
        app.MapGet("/api/projects/{name}", async (string name, IProjectStore store) =>
        {
            var config = await store.LoadAsync(name).ConfigureAwait(false);
            return config is null ? Results.NotFound() : Results.Ok(config);
        });

        // List available files for a project
        app.MapGet("/api/projects/{name}/files", (string name, IProjectStore store) =>
        {
            var dir = store.GetProjectDirectory(name);
            if (!Directory.Exists(dir))
                return Results.NotFound();

            var knowledge = Directory.Exists(Path.Combine(dir, "knowledge"))
                ? Directory.GetFiles(Path.Combine(dir, "knowledge"), "*.json").Select(Path.GetFileName).ToList()
                : new List<string?>();

            var output = Directory.Exists(Path.Combine(dir, "output"))
                ? Directory.GetFiles(Path.Combine(dir, "output")).Select(Path.GetFileName).ToList()
                : new List<string?>();

            var diagrams = Directory.Exists(Path.Combine(dir, "output", "diagrams"))
                ? Directory.GetFiles(Path.Combine(dir, "output", "diagrams"), "*.mmd").Select(Path.GetFileName).ToList()
                : new List<string?>();

            return Results.Ok(new { knowledge, output, diagrams });
        });

        // Get project knowledge file
        app.MapGet("/api/projects/{name}/knowledge/{file}", async (string name, string file, IProjectStore store) =>
        {
            if (!IsValidFileName(file))
                return Results.BadRequest("Invalid file name.");

            var dir = store.GetProjectDirectory(name);
            var path = Path.Combine(dir, "knowledge", file);
            if (!File.Exists(path))
                return Results.NotFound();

            var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return Results.Content(content, "application/json");
        });

        // Get project output file (narrative.md, api.yaml, client-prompt.md, etc.)
        app.MapGet("/api/projects/{name}/output/{file}", async (string name, string file, IProjectStore store) =>
        {
            if (!IsValidFileName(file))
                return Results.BadRequest("Invalid file name.");

            var dir = store.GetProjectDirectory(name);
            var path = Path.Combine(dir, "output", file);
            if (!File.Exists(path))
                return Results.NotFound();

            var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var contentType = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json"
                : file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ? "text/yaml"
                : file.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "text/html"
                : "text/plain";
            return Results.Content(content, contentType);
        });

        // Get diagram file
        app.MapGet("/api/projects/{name}/diagrams/{file}", async (string name, string file, IProjectStore store) =>
        {
            if (!IsValidFileName(file))
                return Results.BadRequest("Invalid file name.");

            var dir = store.GetProjectDirectory(name);
            var path = Path.Combine(dir, "output", "diagrams", file);
            if (!File.Exists(path))
                return Results.NotFound();

            var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return Results.Content(content, "text/plain");
        });

        // Update project status
        app.MapPost("/api/projects/{name}/status", async (string name, StatusUpdate update, IProjectStore store) =>
        {
            var config = await store.LoadAsync(name).ConfigureAwait(false);
            if (config is null)
                return Results.NotFound();

            if (!Enum.TryParse<ProjectStatus>(update.Status, ignoreCase: true, out var status))
                return Results.BadRequest($"Invalid status: {update.Status}");

            var updated = config with { Status = status, LastActivityAt = DateTimeOffset.UtcNow };
            await store.SaveAsync(updated).ConfigureAwait(false);
            return Results.Ok(updated);
        });
    }

    /// <summary>
    /// Validates that a file name does not contain path traversal characters.
    /// </summary>
    private static bool IsValidFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && !fileName.Contains("..", StringComparison.Ordinal)
        && !fileName.Contains('/', StringComparison.Ordinal)
        && !fileName.Contains('\\', StringComparison.Ordinal)
        && fileName == Path.GetFileName(fileName);

    /// <summary>Status update request body.</summary>
    internal sealed record StatusUpdate(string Status);
}
