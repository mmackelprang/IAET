using Microsoft.Extensions.DependencyInjection;

namespace Iaet.ProtocolAnalysis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetProtocolAnalysis(this IServiceCollection services)
    {
        services.AddSingleton<IStreamAnalyzer, WebSocketAnalyzer>();
        return services;
    }
}
