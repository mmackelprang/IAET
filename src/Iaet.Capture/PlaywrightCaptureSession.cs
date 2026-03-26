using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Playwright;

namespace Iaet.Capture;

public sealed class PlaywrightCaptureSession : ICaptureSession
{
    private readonly CaptureOptions _options;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private CdpNetworkListener? _listener;

    public Guid SessionId { get; } = Guid.NewGuid();
    public string TargetApplication => _options.TargetApplication;
    public bool IsRecording { get; private set; }

    public PlaywrightCaptureSession(CaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task StartAsync(string url, CancellationToken ct = default)
    {
        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            Args = [$"--profile-directory={_options.Profile}"]
        }).ConfigureAwait(false);

        _page = await _browser.NewPageAsync().ConfigureAwait(false);
        _listener = new CdpNetworkListener(SessionId);
        _listener.Attach(_page);
        IsRecording = true;

        await _page.GotoAsync(url).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        IsRecording = false;
        if (_browser is not null)
        {
            try { await _browser.CloseAsync().ConfigureAwait(false); }
            catch (PlaywrightException) { /* browser may already be closed */ }
        }
        _playwright?.Dispose();
    }

    public async IAsyncEnumerable<CapturedRequest> GetCapturedRequestsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_listener is null) yield break;
        foreach (var request in _listener.DrainCaptured())
        {
            yield return request;
        }
        await Task.CompletedTask.ConfigureAwait(false); // satisfy async requirement
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRecording) await StopAsync().ConfigureAwait(false);
    }
}
