using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Agents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetAgents(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton(_ => new InvestigationLog(rootDirectory));
        services.AddSingleton<HumanInteractionBroker>();
        return services;
    }
}
