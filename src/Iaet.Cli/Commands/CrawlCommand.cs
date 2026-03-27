using System.CommandLine;
using System.Text.Json;
using Iaet.Capture;
using Iaet.Catalog;
using Iaet.Crawler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

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

            var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            try
            {
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = options.Headless
                }).ConfigureAwait(false);

                try
                {
                    var page = await browser.NewPageAsync().ConfigureAwait(false);
                    var navigator = new PlaywrightPageNavigator(page);
                    var engine = new CrawlEngine(options, navigator);

                    Console.WriteLine("Crawling...");
                    var report = await engine.RunAsync().ConfigureAwait(false);

                    Console.WriteLine($"Crawl complete — {report.Pages.Count} page(s) discovered.");

                    var json = JsonSerializer.Serialize(report, JsonOptions);

                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        await File.WriteAllTextAsync(outputPath, json).ConfigureAwait(false);
                        Console.WriteLine($"Report written to: {outputPath}");
                    }
                    else
                    {
                        Console.WriteLine(json);
                    }
                }
                finally
                {
                    try { await browser.CloseAsync().ConfigureAwait(false); }
                    catch (PlaywrightException) { /* browser may already be closed */ }
                }
            }
            finally
            {
                playwright.Dispose();
            }
        });

        return crawlCmd;
    }
}
