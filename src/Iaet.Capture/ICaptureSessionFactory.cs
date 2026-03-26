using Iaet.Core.Abstractions;

namespace Iaet.Capture;

public interface ICaptureSessionFactory
{
    ICaptureSession Create(CaptureOptions options);
    ICaptureSession Create(CaptureOptions options, IStreamCatalog? streamCatalog,
        IReadOnlyList<IProtocolListener>? listeners);
}

public sealed class PlaywrightCaptureSessionFactory : ICaptureSessionFactory
{
    public ICaptureSession Create(CaptureOptions options) =>
        new PlaywrightCaptureSession(options);

    public ICaptureSession Create(CaptureOptions options, IStreamCatalog? streamCatalog,
        IReadOnlyList<IProtocolListener>? listeners) =>
        new PlaywrightCaptureSession(options, streamCatalog, listeners);
}
