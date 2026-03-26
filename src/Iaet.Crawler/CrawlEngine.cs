using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Iaet.Core.Models;

namespace Iaet.Crawler;

public sealed class CrawlEngine
{
    private readonly CrawlOptions _options;
    private readonly IPageNavigator _navigator;

    public CrawlEngine(CrawlOptions options, IPageNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(navigator);
        _options = options;
        _navigator = navigator;
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
        Justification = "URL stored as plain string for serialization compatibility.")]
    public async Task<CrawlReport> RunAsync(CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var timeout = TimeSpan.FromSeconds(_options.MaxDurationSeconds);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        var linkedCt = cts.Token;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pages = new List<DiscoveredPage>();
        var queue = new Queue<(string Url, int Depth)>();
        queue.Enqueue((_options.StartUrl, 0));

        var interactor = new PageInteractor(_navigator);
        var sw = Stopwatch.StartNew();

        while (queue.Count > 0 && pages.Count < _options.MaxPages && sw.Elapsed < timeout)
        {
            if (linkedCt.IsCancellationRequested)
                break;

            var (url, depth) = queue.Dequeue();

            if (!visited.Add(url))
                continue;

            if (!_options.IsUrlAllowed(url))
                continue;

            if (depth > _options.MaxDepth)
                continue;

            IElementQueryable queryable;
            try
            {
                queryable = await _navigator.NavigateAsync(url, linkedCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var elements = await ElementDiscoverer.DiscoverAsync(queryable, _options, linkedCt).ConfigureAwait(false);
            var navigatedTo = new List<string>();

            foreach (var element in elements)
            {
                InteractionResult result;
                try
                {
                    result = await interactor.InteractAsync(element, linkedCt).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result.NavigatedTo is not null &&
                    !visited.Contains(result.NavigatedTo))
                {
                    navigatedTo.Add(result.NavigatedTo);
                    queue.Enqueue((result.NavigatedTo, depth + 1));
                }

                if (result.UrlChanged)
                {
                    // SPA navigation occurred — break and let BFS handle the new page
                    break;
                }
            }

            IReadOnlyList<string> apiCalls;
            try
            {
                apiCalls = await _navigator
                    .GetApiCallsSinceLastNavigationAsync(linkedCt)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                apiCalls = [];
            }

            pages.Add(new DiscoveredPage
            {
                Url = url,
                Depth = depth,
                InteractiveElements = elements,
                ApiCallsTriggered = apiCalls,
                NavigatedTo = navigatedTo
            });
        }

        return new CrawlReport
        {
            SessionId = Guid.NewGuid(),
            TargetApplication = _options.TargetApplication,
            StartUrl = _options.StartUrl,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Pages = pages,
            TotalRequestsCaptured = pages.Sum(p => p.ApiCallsTriggered.Count),
            TotalStreamsCaptured = 0
        };
    }
}
