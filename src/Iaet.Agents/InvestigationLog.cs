using System.Globalization;

namespace Iaet.Agents;

public sealed class InvestigationLog(string rootDirectory) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task AppendAsync(string projectName, string agent, string message, CancellationToken ct = default)
    {
        var logPath = Path.Combine(rootDirectory, projectName, "investigation.log");
        var timestamp = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var line = $"{timestamp} [{agent}] {message}";

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllLinesAsync(logPath, [line], ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();
}
