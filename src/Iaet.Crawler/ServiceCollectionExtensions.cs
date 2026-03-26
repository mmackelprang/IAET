using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Crawler;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCrawler(this IServiceCollection services)
    {
        // ElementDiscoverer and RecipeRunner are constructed manually — not registered via DI.
        // ElementDiscoverer requires CrawlOptions passed per-call to DiscoverAsync.
        // RecipeRunner is entirely static and has no instance state.
        return services;
    }
}
