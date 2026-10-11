using Iaet.Core.Abstractions;
using Iaet.Native.Ghidra;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Native;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetNative(this IServiceCollection services)
    {
        services.AddSingleton<IToolProvider, GhidraToolProvider>();
        return services;
    }
}
