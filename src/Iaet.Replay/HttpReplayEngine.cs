using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Extensions.Logging;

namespace Iaet.Replay;

/// <summary>
/// Replays a <see cref="CapturedRequest"/> against its original URL, compares the live
/// response to the captured one, and returns a <see cref="ReplayResult"/> with any diffs.
/// </summary>
public sealed class HttpReplayEngine : IReplayEngine, IDisposable
{
    private static readonly HashSet<string> RedactedMarkers =
        new(StringComparer.OrdinalIgnoreCase) { "<REDACTED>" };

    // CA1848 / CA1873: use compile-time LoggerMessage delegates
    private static readonly Action<ILogger, string, Exception?> LogDryRun =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "DryRun"),
            "DryRun enabled — skipping HTTP call for {Url}");

    private static readonly Action<ILogger, string, Exception?> LogMinuteLimit =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "MinuteLimit"),
            "Per-minute rate limit reached for {Url}");

    private static readonly Action<ILogger, string, Exception?> LogDayLimit =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, "DayLimit"),
            "Per-day rate limit reached for {Url}");

    private static readonly Action<ILogger, string, string, int, long, int, Exception?> LogReplayed =
        LoggerMessage.Define<string, string, int, long, int>(LogLevel.Debug, new EventId(4, "Replayed"),
            "Replayed {Method} {Url} → {Status} in {Ms}ms with {DiffCount} diffs");

    private readonly HttpClient                _httpClient;
    private readonly ReplayOptions             _options;
    private readonly IReplayAuthProvider?      _authProvider;
    private readonly ILogger<HttpReplayEngine>? _logger;

    private readonly FixedWindowRateLimiter _minuteLimiter;
    private readonly FixedWindowRateLimiter _dayLimiter;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="HttpReplayEngine"/>.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send replayed requests.</param>
    /// <param name="options">Configuration options controlling rate limits, timeout, and dry-run mode.</param>
    /// <param name="authProvider">Optional provider that attaches auth credentials before each request.</param>
    /// <param name="logger">Optional logger.</param>
    public HttpReplayEngine(
        HttpClient                  httpClient,
        ReplayOptions               options,
        IReplayAuthProvider?        authProvider = null,
        ILogger<HttpReplayEngine>?  logger       = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient   = httpClient;
        _options      = options;
        _authProvider = authProvider;
        _logger       = logger;

        _minuteLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit          = options.RequestsPerMinute,
            Window               = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
        });

        _dayLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit          = options.RequestsPerDay,
            Window               = TimeSpan.FromDays(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
        });
    }

    /// <inheritdoc />
    public async Task<ReplayResult> ReplayAsync(
        CapturedRequest original,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (_options.DryRun)
        {
            if (_logger is not null)
            {
                LogDryRun(_logger, original.Url, null);
            }

            return new ReplayResult(0, null, [], 0);
        }

        // Rate limiting — acquire permits from both windows before proceeding.
        using var minuteLease = await _minuteLimiter.AcquireAsync(1, ct).ConfigureAwait(false);
        if (!minuteLease.IsAcquired)
        {
            if (_logger is not null)
            {
                LogMinuteLimit(_logger, original.Url, null);
            }

            throw new InvalidOperationException("Per-minute rate limit exceeded.");
        }

        using var dayLease = await _dayLimiter.AcquireAsync(1, ct).ConfigureAwait(false);
        if (!dayLease.IsAcquired)
        {
            if (_logger is not null)
            {
                LogDayLimit(_logger, original.Url, null);
            }

            throw new InvalidOperationException("Per-day rate limit exceeded.");
        }

        // Build + send request
        using var request = BuildRequest(original);

        if (_authProvider is not null)
        {
            await _authProvider.ApplyAuthAsync(request, ct).ConfigureAwait(false);
        }

        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        sw.Stop();

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            responseBody = null;
        }

        // Compare status
        var diffs        = new List<FieldDiff>();
        var actualStatus = (int)response.StatusCode;

        if (actualStatus != original.ResponseStatus)
        {
            diffs.Add(new FieldDiff(
                "$.status",
                original.ResponseStatus.ToString(CultureInfo.InvariantCulture),
                actualStatus.ToString(CultureInfo.InvariantCulture)));
        }

        // Compare body
        diffs.AddRange(JsonDiffer.Diff(original.ResponseBody, responseBody));

        if (_logger is not null)
        {
            LogReplayed(_logger, original.HttpMethod, original.Url, actualStatus,
                sw.ElapsedMilliseconds, diffs.Count, null);
        }

        return new ReplayResult(actualStatus, responseBody, diffs, sw.ElapsedMilliseconds);
    }

    private static HttpRequestMessage BuildRequest(CapturedRequest original)
    {
        var method  = new HttpMethod(original.HttpMethod);
        var request = new HttpRequestMessage(method, original.Url);

        foreach (var (key, value) in original.RequestHeaders)
        {
            if (RedactedMarkers.Contains(value))
            {
                continue;
            }

            // Content headers must be set on Content; try request headers first
            if (!request.Headers.TryAddWithoutValidation(key, value))
            {
                request.Content ??= new StringContent(string.Empty);
                request.Content.Headers.TryAddWithoutValidation(key, value);
            }
        }

        if (original.RequestBody is { Length: > 0 })
        {
            request.Content = new StringContent(
                original.RequestBody,
                System.Text.Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _minuteLimiter.Dispose();
        _dayLimiter.Dispose();
    }
}
