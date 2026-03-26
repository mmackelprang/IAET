using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Schema;

/// <summary>
/// Extension methods for registering Iaet.Schema services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="JsonSchemaInferrer"/> as the <see cref="ISchemaInferrer"/> implementation.
    /// </summary>
    public static IServiceCollection AddIaetSchema(this IServiceCollection services)
    {
        services.AddSingleton<ISchemaInferrer, JsonSchemaInferrer>();
        return services;
    }
}
