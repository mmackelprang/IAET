using Iaet.Catalog;
using Iaet.Explorer.Api;
using Iaet.Replay;
using Iaet.Schema;
using Iaet.Export;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Iaet.Explorer;

/// <summary>
/// Builds and configures the IAET Explorer web application.
/// </summary>
public static class ExplorerApp
{
    /// <summary>
    /// Builds a <see cref="WebApplication"/> configured with the IAET Explorer UI and API.
    /// </summary>
    /// <param name="dbPath">Path to the SQLite catalog database file.</param>
    /// <param name="port">The port to listen on (default 9200).</param>
    /// <returns>A configured <see cref="WebApplication"/> ready to run.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "dbPath is a file path, not a URI")]
    public static WebApplication Build(string dbPath, int port = 9200)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls($"http://localhost:{port}");

        builder.Services.AddRazorPages();
        builder.Services.AddIaetCatalog($"DataSource={dbPath}");
        builder.Services.AddIaetSchema();
        builder.Services.AddIaetReplay();
        builder.Services.AddIaetExport();

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseRouting();
        app.MapRazorPages();

        SessionsApi.Map(app);
        EndpointsApi.Map(app);
        StreamsApi.Map(app);
        SchemaApi.Map(app);
        ReplayApi.Map(app);
        ExportApi.Map(app);

        return app;
    }
}
