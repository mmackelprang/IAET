namespace Iaet.Core.Abstractions;

public interface IReplayAuthProvider
{
    Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default);
}
