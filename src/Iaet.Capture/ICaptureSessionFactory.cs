using Iaet.Core.Abstractions;

namespace Iaet.Capture;

public interface ICaptureSessionFactory
{
    ICaptureSession Create(CaptureOptions options);
}

public sealed class PlaywrightCaptureSessionFactory : ICaptureSessionFactory
{
    public ICaptureSession Create(CaptureOptions options) => new PlaywrightCaptureSession(options);
}
