using System.CommandLine;
using System.Text.Json;
using Iaet.Catalog;
using Iaet.Crawler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class CrawlCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static Command Create(IServiceProvider services)
    {
        var crawlCmd = new Command("crawl", "Semi-autonomous crawler that discovers interactive elements and API calls");

        var urlOption             = new Option<string>("--url")              { Description = "Starting URL to crawl", Required = true };
        var targetOption          = new Option<string>("--target")           { Description = "Target application name", DefaultValueFactory = _ => "Unknown" };
        var sessionOption         = new Option<string>("--session")          { Description = "Session name (auto-generated if omitted)", DefaultValueFactory = _ => Guid.NewGuid().ToString("N")[..8] };
        var maxDepthOption        = new Option<int>("--max-depth")           { Description = "Maximum link-follow depth", DefaultValueFactory = _ => 3 };
        var maxPagesOption        = new Option<int>("--max-pages")           { Description = "Maximum pages to visit", DefaultValueFactory = _ => 50 };
        var maxDurationOption     = new Option<int>("--max-duration")        { Description = "Maximum crawl duration in seconds", DefaultValueFactory = _ => 300 };
        var headlessOption        = new Option<bool>("--headless")           { Description = "Run browser in headless mode" };
        var blacklistOption       = new Option<string[]>("--blacklist")      { Description = "URL path patterns to exclude (repeatable)", AllowMultipleArgumentsPerToken = false };
        var excludeSelectorOption = new Option<string[]>("--exclude-selector") { Description = "CSS selectors to skip during element discovery (repeatable)", AllowMultipleArgumentsPerToken = false };
        var outputOption          = new Option<string?>("--output")          { Description = "Write crawl report JSON to this file path" };

        crawlCmd.Add(urlOption);
        crawlCmd.Add(targetOption);
        crawlCmd.Add(sessionOption);
        crawlCmd.Add(maxDepthOption);
        crawlCmd.Add(maxPagesOption);
        crawlCmd.Add(maxDurationOption);
        crawlCmd.Add(headlessOption);
        crawlCmd.Add(blacklistOption);
        crawlCmd.Add(excludeSelectorOption);
        crawlCmd.Add(outputOption);

        crawlCmd.SetAction(async (parseResult) =>
        {
            var url              = parseResult.GetRequiredValue(urlOption);
            var target           = parseResult.GetValue(targetOption) ?? "Unknown";
            var session          = parseResult.GetValue(sessionOption) ?? Guid.NewGuid().ToString("N")[..8];
            var maxDepth         = parseResult.GetValue(maxDepthOption);
            var maxPages         = parseResult.GetValue(maxPagesOption);
            var maxDuration      = parseResult.GetValue(maxDurationOption);
            var headless         = parseResult.GetValue(headlessOption);
            var blacklist        = parseResult.GetValue(blacklistOption) ?? [];
            var excludeSelectors = parseResult.GetValue(excludeSelectorOption) ?? [];
            var outputPath       = parseResult.GetValue(outputOption);

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);

            var options = new CrawlOptions
            {
                StartUrl             = url,
                TargetApplication    = target,
                MaxDepth             = maxDepth,
                MaxPages             = maxPages,
                MaxDurationSeconds   = maxDuration,
                Headless             = headless,
                UrlBlacklistPatterns = blacklist,
                ExcludedSelectors    = excludeSelectors
            };

            Console.WriteLine($"Crawl session '{session}' — target: {target}");
            Console.WriteLine($"  Start URL:    {options.StartUrl}");
            Console.WriteLine($"  Max depth:    {options.MaxDepth}  |  Max pages: {options.MaxPages}  |  Max duration: {options.MaxDurationSeconds}s");
            Console.WriteLine($"  Headless:     {options.Headless}");

            if (blacklist.Length > 0)
                Console.WriteLine($"  Blacklist:    {string.Join(", ", blacklist)}");
            if (excludeSelectors.Length > 0)
                Console.WriteLine($"  Exclude CSS:  {string.Join(", ", excludeSelectors)}");

            Console.WriteLine();
            Console.WriteLine("DRY RUN: The crawl engine requires a live Playwright browser instance.");
            Console.WriteLine("         No crawl was performed. To run a real crawl, wire up");
            Console.WriteLine("         PlaywrightPageNavigator (Iaet.Capture) and pass it to CrawlEngine:");
            Console.WriteLine("           var engine = new CrawlEngine(options, navigator);");
            Console.WriteLine("           var report = await engine.RunAsync(ct);");
            Console.WriteLine();
            Console.WriteLine("Exit code 2 signals dry-run / no browser integration available.");

            if (!string.IsNullOrEmpty(outputPath))
            {
                // Write a placeholder report so the --output path is honoured
                var placeholder = new { Session = session, Options = options, Status = "pending-browser-integration" };
                var json = JsonSerializer.Serialize(placeholder, JsonOptions);
                await File.WriteAllTextAsync(outputPath, json).ConfigureAwait(false);
                Console.WriteLine($"Report placeholder written to: {outputPath}");
            }

            Environment.Exit(2);
        });

        return crawlCmd;
    }
}
