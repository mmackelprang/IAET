using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IProtocolListener
{
    string ProtocolName { get; }
    StreamProtocol Protocol { get; }
    bool CanAttach(ICdpSession cdpSession);
    Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default);
    Task DetachAsync(CancellationToken ct = default);
    IReadOnlyList<CapturedStream> GetPendingStreams();
}
