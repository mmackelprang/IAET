using System.CommandLine;
using System.Globalization;
using Iaet.Capture;
using Iaet.Catalog;
using Iaet.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File("logs/iaet-.log", rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        var dbPath = context.Configuration["Iaet:DatabasePath"] ?? "catalog.db";
        services.AddIaetCapture();
        services.AddIaetCatalog($"DataSource={dbPath}");
    })
    .Build();

var rootCommand = new RootCommand("IAET - Internal API Extraction Toolkit")
{
    CaptureCommand.Create(host.Services),
    CatalogCommand.Create(host.Services),
    StreamsCommand.Create(host.Services)
};

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
