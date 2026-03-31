using System.CommandLine;
using System.Globalization;
using Iaet.Agents;
using Iaet.Android;
using Iaet.Capture;
using Iaet.Cookies;
using Iaet.Diagrams;
using Iaet.Catalog;
using Iaet.Cli.Commands;
using Iaet.Crawler;
using Iaet.Export;
using Iaet.Projects;
using Iaet.Replay;
using Iaet.Schema;
using Iaet.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                     standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose,
                     formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File("logs/iaet-.log", rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        var dbPath = context.Configuration["Iaet:DatabasePath"] ?? "catalog.db";
        services.AddIaetCapture();
        services.AddIaetCatalog($"DataSource={dbPath}");
        services.AddIaetSchema();
        services.AddIaetReplay();
        services.AddIaetExport();
        services.AddIaetCrawler();

        var projectsRoot = Path.Combine(Directory.GetCurrentDirectory(), ".iaet-projects");
        services.AddIaetProjects(projectsRoot);
        services.AddIaetSecrets(projectsRoot);
        services.AddIaetAgents(projectsRoot);
        services.AddIaetCookies(projectsRoot);
        services.AddIaetDiagrams();
        services.AddIaetAndroid();
    })
    .Build();

var rootCommand = new RootCommand("IAET - Internal API Extraction Toolkit")
{
    CaptureCommand.Create(host.Services),
    CatalogCommand.Create(host.Services),
    StreamsCommand.Create(host.Services),
    SchemaCommand.Create(host.Services),
    ReplayCommand.Create(host.Services),
    ExportCommand.Create(host.Services),
    ImportCommand.Create(host.Services),
    CrawlCommand.Create(host.Services),
    ExploreCommand.Create(),
    InvestigateCommand.Create(host.Services),
    ProjectCommand.Create(host.Services),
    SecretsCommand.Create(host.Services),
    RoundCommand.Create(host.Services),
    CookiesCommand.Create(host.Services),
    DashboardCommand.Create(host.Services),
    ApkCommand.Create(host.Services)
};

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
