using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Export;

/// <summary>
/// Extension methods for registering IAET Export services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all <c>Iaet.Export</c> services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddIaetExport(this IServiceCollection services)
    {
        return services;
    }
}
