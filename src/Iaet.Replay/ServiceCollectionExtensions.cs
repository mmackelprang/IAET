using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Replay;

/// <summary>
/// Extension methods for registering IAET replay services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpReplayEngine"/> as the <see cref="IReplayEngine"/> implementation,
    /// wired up with the standard resilience handler and the supplied <paramref name="configure"/> options.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional delegate to override <see cref="ReplayOptions"/> defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddIaetReplay(
        this IServiceCollection services,
        Action<ReplayOptions>? configure = null)
    {
        var options = new ReplayOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddHttpClient<IReplayEngine, HttpReplayEngine>()
                .AddStandardResilienceHandler();
        return services;
    }
}
