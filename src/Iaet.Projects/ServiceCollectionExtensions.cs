using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Projects;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetProjects(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton<IProjectStore>(new ProjectStore(rootDirectory));
        return services;
    }
}
