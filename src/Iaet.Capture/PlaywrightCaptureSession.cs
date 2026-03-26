using Iaet.Capture.Cdp;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Playwright;

namespace Iaet.Capture;

public sealed class PlaywrightCaptureSession : ICaptureSession
{
    private readonly CaptureOptions _options;
    private readonly IStreamCatalog? _streamCatalog;
    private readonly IReadOnlyList<IProtocolListener> _listeners;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private CdpNetworkListener? _listener;
    private PlaywrightCdpSession? _cdpSession;

    public Guid SessionId { get; } = Guid.NewGuid();
    public string TargetApplication => _options.TargetApplication;
    public bool IsRecording { get; private set; }

    public PlaywrightCaptureSession(CaptureOptions options,
        IStreamCatalog? streamCatalog = null,
        IReadOnlyList<IProtocolListener>? listeners = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _streamCatalog = streamCatalog;
        _listeners = listeners ?? [];
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

        if (_options.Streams.Enabled && _listeners.Count > 0)
        {
            _cdpSession = await PlaywrightCdpSession.CreateAsync(_page, ct).ConfigureAwait(false);
            foreach (var listener in _listeners)
            {
                if (listener.CanAttach(_cdpSession))
                {
                    await listener.AttachAsync(_cdpSession, _streamCatalog!, ct).ConfigureAwait(false);
                }
            }
        }

        IsRecording = true;

        await _page.GotoAsync(url).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        IsRecording = false;

        if (_options.Streams.Enabled && _streamCatalog is not null)
        {
            foreach (var listener in _listeners)
            {
                await listener.DetachAsync(ct).ConfigureAwait(false);
                foreach (var stream in listener.GetPendingStreams())
                {
                    await _streamCatalog.SaveStreamAsync(stream, ct).ConfigureAwait(false);
                }
            }
        }

        if (_browser is not null)
        {
            try { await _browser.CloseAsync().ConfigureAwait(false); }
            catch (PlaywrightException) { /* browser may already be closed */ }
        }
        _playwright?.Dispose();

        if (_cdpSession is not null)
        {
            await _cdpSession.DisposeAsync().ConfigureAwait(false);
        }
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
        GC.SuppressFinalize(this);
    }
}
