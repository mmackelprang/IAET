using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Capture;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCapture(this IServiceCollection services)
    {
        services.AddSingleton<ICaptureSessionFactory, PlaywrightCaptureSessionFactory>();
        // Protocol listeners are constructed per-session in CaptureCommand
        // because their StreamCaptureOptions come from CLI arguments, not DI
        return services;
    }
}
