using Iaet.Capture.Listeners;
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Capture;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCapture(this IServiceCollection services)
    {
        services.AddSingleton<ICaptureSessionFactory, PlaywrightCaptureSessionFactory>();
        services.AddTransient<WebSocketListener>(sp =>
            new WebSocketListener(sp.GetRequiredService<StreamCaptureOptions>()));
        services.AddTransient<SseListener>(sp =>
            new SseListener(sp.GetRequiredService<StreamCaptureOptions>()));
        services.AddTransient<MediaStreamListener>(sp =>
            new MediaStreamListener(sp.GetRequiredService<StreamCaptureOptions>()));
        services.AddTransient<GrpcWebListener>(sp =>
            new GrpcWebListener(sp.GetRequiredService<StreamCaptureOptions>()));
        services.AddTransient<WebRtcListener>(sp =>
            new WebRtcListener(sp.GetRequiredService<StreamCaptureOptions>()));
        return services;
    }
}
