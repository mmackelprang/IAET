using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Capture;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCapture(this IServiceCollection services)
    {
        services.AddSingleton<ICaptureSessionFactory, PlaywrightCaptureSessionFactory>();
        return services;
    }
}
