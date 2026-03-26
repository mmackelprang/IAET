using System.Collections.Concurrent;
using System.Diagnostics;
using Iaet.Core.Models;
using Microsoft.Playwright;

namespace Iaet.Capture;

public sealed class CdpNetworkListener
{
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();
    private readonly ConcurrentQueue<CapturedRequest> _completed = new();
    private readonly Guid _sessionId;
    private long _nextRequestId;

    public CdpNetworkListener(Guid sessionId)
    {
        _sessionId = sessionId;
    }

    public void Attach(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        page.Request += (_, request) =>
        {
            if (!IsXhrOrFetch(request)) return;
            var id = Interlocked.Increment(ref _nextRequestId);
            var key = $"{request.Method}:{request.Url}:{id}";
            _pending[key] = new PendingRequest(Stopwatch.StartNew(), request, key);
        };

        page.Response += async (_, response) =>
        {
            var prefix = $"{response.Request.Method}:{response.Request.Url}:";
            var matchKey = _pending.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(k => long.Parse(k[prefix.Length..], System.Globalization.CultureInfo.InvariantCulture))
                .FirstOrDefault();
            if (matchKey is null || !_pending.TryRemove(matchKey, out var pending)) return;
            pending.Stopwatch.Stop();

            string? requestBody = null;
            try { requestBody = response.Request.PostData; }
#pragma warning disable CA1031 // Intentionally catching all exceptions: some requests have no body and may throw unpredictably
            catch { /* some requests have no body */ }
#pragma warning restore CA1031

            string? responseBody = null;
            try { responseBody = await response.TextAsync().ConfigureAwait(false); }
#pragma warning disable CA1031 // Intentionally catching all exceptions: binary or failed responses may throw unpredictably
            catch { /* binary or failed */ }
#pragma warning restore CA1031

            var captured = new CapturedRequest
            {
                Id = Guid.NewGuid(),
                SessionId = _sessionId,
                Timestamp = DateTimeOffset.UtcNow,
                HttpMethod = response.Request.Method,
                Url = response.Request.Url,
                RequestHeaders = RequestSanitizer.SanitizeHeaders(
                    await response.Request.AllHeadersAsync().ConfigureAwait(false)),
                RequestBody = requestBody,
                ResponseStatus = response.Status,
                ResponseHeaders = RequestSanitizer.SanitizeHeaders(
                    await response.AllHeadersAsync().ConfigureAwait(false)),
                ResponseBody = responseBody,
                DurationMs = pending.Stopwatch.ElapsedMilliseconds,
            };

            _completed.Enqueue(captured);
        };
    }

    public IReadOnlyList<CapturedRequest> DrainCaptured()
    {
        var result = new List<CapturedRequest>();
        while (_completed.TryDequeue(out var item))
            result.Add(item);
        return result;
    }

    private static bool IsXhrOrFetch(IRequest request) =>
        request.ResourceType is "xhr" or "fetch";

    private sealed record PendingRequest(Stopwatch Stopwatch, IRequest Request, string Key);
}
