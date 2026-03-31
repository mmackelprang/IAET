namespace Iaet.Core.Models;

public enum StreamProtocol
{
    Unknown,
    WebSocket,
    ServerSentEvents,
    WebRtc,
    HlsStream,
    DashStream,
    GrpcWeb,
    WebAudio,
    Hls = HlsStream,
    Dash = DashStream,
}
