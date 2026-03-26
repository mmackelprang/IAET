using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Crawler;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCrawler(this IServiceCollection services)
    {
        services.AddTransient<ElementDiscoverer>();
        services.AddTransient<RecipeRunner>();
        return services;
    }
}
