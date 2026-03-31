using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Secrets;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetSecrets(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton<ISecretsStore>(new DotEnvSecretsStore(rootDirectory));
        services.AddSingleton<ISecretsRedactor, SecretsRedactor>();
        return services;
    }
}
