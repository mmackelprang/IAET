using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cookies;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCookies(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton<ICookieStore>(new FileCookieStore(rootDirectory));
        return services;
    }
}
