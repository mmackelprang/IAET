# IAET Phase 5: Semi-Autonomous Crawler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a semi-autonomous web crawler that systematically discovers pages and API endpoints by interacting with a web application, plus a recipe runner for script-driven capture using TypeScript Playwright scripts.

**Architecture:** `Iaet.Crawler` orchestrates Playwright for DOM discovery and interaction while `Iaet.Capture` handles API traffic capture. The crawler discovers interactive elements (links, buttons, forms), interacts with them within configurable boundary rules, and builds a `CrawlReport` mapping pages to the API calls they trigger. The recipe runner spawns a Node.js subprocess that connects to the same browser instance via CDP.

**Tech Stack:** .NET 10, Playwright .NET (DOM interaction + CDP), Node.js (recipe execution), xUnit + FluentAssertions + NSubstitute

**Spec:** See design spec Sections 5.3 and 14

**IMPORTANT:** All work on branch `phase5-crawler`. Create PR to main when complete. Run comprehensive code review before merging.

---

## Phase 5 Scope

By the end of this phase:
- `CrawlOptions` — boundary rules (URL patterns, max depth/pages/duration, excluded selectors, form strategy)
- `ElementDiscoverer` — finds interactive elements on a page via Playwright DOM queries
- `PageInteractor` — clicks/fills/navigates discovered elements while tracking state
- `PageInteractor` — clicks buttons, fills forms, tracks navigation changes for SPA support
- `CrawlEngine` — orchestrates discovery + interaction + capture across pages
- `CrawlReport` model — discovered pages, interaction graph, per-page API calls
- `RecipeRunner` — spawns Node.js to execute TypeScript Playwright recipes
- `iaet crawl` CLI command with boundary options
- `iaet capture run --recipe <path>` CLI command
- Tests for all components

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `src/Iaet.Core/Models/CrawlReport.cs` | Create | Crawl report model |
| `src/Iaet.Crawler/CrawlOptions.cs` | Create | Boundary rules configuration |
| `src/Iaet.Crawler/ElementDiscoverer.cs` | Create | DOM element discovery |
| `src/Iaet.Crawler/PageInteractor.cs` | Create | Page interaction engine |
| `src/Iaet.Crawler/CrawlEngine.cs` | Create | Orchestrator |
| `src/Iaet.Crawler/RecipeRunner.cs` | Create | TypeScript recipe execution |
| `src/Iaet.Crawler/ServiceCollectionExtensions.cs` | Create | DI registration |
| `src/Iaet.Cli/Commands/CrawlCommand.cs` | Create | `iaet crawl` CLI |
| `src/Iaet.Cli/Commands/CaptureCommand.cs` | Modify | Add `--recipe` to capture run |
| `src/Iaet.Cli/Program.cs` | Modify | Register CrawlCommand + DI |
| `tests/Iaet.Crawler.Tests/CrawlOptionsTests.cs` | Create | Options validation tests |
| `tests/Iaet.Crawler.Tests/ElementDiscovererTests.cs` | Create | Element discovery tests |
| `tests/Iaet.Crawler.Tests/CrawlEngineTests.cs` | Create | Orchestration logic tests |
| `tests/Iaet.Crawler.Tests/RecipeRunnerTests.cs` | Create | Recipe execution tests |
| `docs/recipes/spotify-playlist-capture.ts` | Create | Example TypeScript recipe |
| `docs/recipes/github-api-discovery.ts` | Create | Another example recipe |

---

## Task 1: Create Branch, CrawlReport Model, and CrawlOptions

**Files:**
- Create: `src/Iaet.Core/Models/CrawlReport.cs`
- Create: `src/Iaet.Crawler/CrawlOptions.cs`
- Create: `tests/Iaet.Crawler.Tests/CrawlOptionsTests.cs`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/prj/IAET
git checkout main && git pull origin main
git checkout -b phase5-crawler
```

- [ ] **Step 2: Set up Crawler test project**

```bash
dotnet new xunit -n Iaet.Crawler.Tests -o tests/Iaet.Crawler.Tests
rm tests/Iaet.Crawler.Tests/UnitTest1.cs
dotnet sln Iaet.slnx add tests/Iaet.Crawler.Tests/Iaet.Crawler.Tests.csproj
dotnet add tests/Iaet.Crawler.Tests reference src/Iaet.Crawler
dotnet add tests/Iaet.Crawler.Tests reference src/Iaet.Core
dotnet add tests/Iaet.Crawler.Tests package FluentAssertions
dotnet add tests/Iaet.Crawler.Tests package NSubstitute
```

Strip redundant csproj properties. Add `IsTestProject=true` and `NoWarn CA1707`. Remove `Placeholder.cs` from `src/Iaet.Crawler/`.

- [ ] **Step 3: Create CrawlReport model in Core**

```csharp
namespace Iaet.Core.Models;

public sealed class CrawlReport
{
    public required Guid SessionId { get; init; }
    public required string TargetApplication { get; init; }
    public required string StartUrl { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required IReadOnlyList<DiscoveredPage> Pages { get; init; }
    public required int TotalRequestsCaptured { get; init; }
    public required int TotalStreamsCaptured { get; init; }
}

public sealed class DiscoveredPage
{
    public required string Url { get; init; }
    public required int Depth { get; init; }
    public required IReadOnlyList<DiscoveredElement> InteractiveElements { get; init; }
    public required IReadOnlyList<string> ApiCallsTriggered { get; init; }
    public required IReadOnlyList<string> NavigatedTo { get; init; }
}

public sealed class DiscoveredElement
{
    public required string TagName { get; init; }
    public required string Selector { get; init; }
    public string? Text { get; init; }
    public string? Href { get; init; }
    public bool WasInteracted { get; init; }
}
```

- [ ] **Step 4: Create CrawlOptions with tests (TDD)**

Tests:
```csharp
public class CrawlOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var opts = new CrawlOptions { StartUrl = "https://example.com" };
        opts.MaxDepth.Should().Be(3);
        opts.MaxPages.Should().Be(50);
        opts.MaxDurationSeconds.Should().Be(300);
        opts.Headless.Should().BeFalse();
    }

    [Fact]
    public void IsUrlAllowed_NoPatterns_AllowsAll()
    {
        var opts = new CrawlOptions { StartUrl = "https://example.com" };
        opts.IsUrlAllowed("https://example.com/page1").Should().BeTrue();
    }

    [Fact]
    public void IsUrlAllowed_WithWhitelist_RestrictsToPattern()
    {
        var opts = new CrawlOptions
        {
            StartUrl = "https://example.com",
            UrlWhitelistPatterns = ["/app/*", "/dashboard/*"]
        };
        opts.IsUrlAllowed("https://example.com/app/settings").Should().BeTrue();
        opts.IsUrlAllowed("https://example.com/admin/users").Should().BeFalse();
    }

    [Fact]
    public void IsUrlAllowed_WithBlacklist_ExcludesPattern()
    {
        var opts = new CrawlOptions
        {
            StartUrl = "https://example.com",
            UrlBlacklistPatterns = ["/logout", "/delete*"]
        };
        opts.IsUrlAllowed("https://example.com/logout").Should().BeFalse();
        opts.IsUrlAllowed("https://example.com/app").Should().BeTrue();
    }

    [Fact]
    public void IsSelectorExcluded_MatchesExcludedSelectors()
    {
        var opts = new CrawlOptions
        {
            StartUrl = "https://example.com",
            ExcludedSelectors = [".delete-btn", "[data-destructive]"]
        };
        opts.IsSelectorExcluded(".delete-btn").Should().BeTrue();
        opts.IsSelectorExcluded(".save-btn").Should().BeFalse();
    }
}
```

Implementation:
```csharp
namespace Iaet.Crawler;

public sealed class CrawlOptions
{
    public required string StartUrl { get; init; }
    public string TargetApplication { get; init; } = "Unknown";
    public int MaxDepth { get; init; } = 3;
    public int MaxPages { get; init; } = 50;
    public int MaxDurationSeconds { get; init; } = 300;
    public bool Headless { get; init; }
    public IReadOnlyList<string> UrlWhitelistPatterns { get; init; } = [];
    public IReadOnlyList<string> UrlBlacklistPatterns { get; init; } = [];
    public IReadOnlyList<string> ExcludedSelectors { get; init; } = [];
    public FormFillStrategy FormStrategy { get; init; } = FormFillStrategy.Skip;
    public bool CaptureStreams { get; init; } = true;

    public bool IsUrlAllowed(string url)
    {
        var path = new Uri(url).AbsolutePath;

        if (UrlBlacklistPatterns.Count > 0 &&
            UrlBlacklistPatterns.Any(p => MatchesPattern(path, p)))
            return false;

        if (UrlWhitelistPatterns.Count > 0)
            return UrlWhitelistPatterns.Any(p => MatchesPattern(path, p));

        return true;
    }

    public bool IsSelectorExcluded(string selector) =>
        ExcludedSelectors.Any(excluded =>
            selector.Contains(excluded, StringComparison.Ordinal) ||
            excluded.Contains(selector, StringComparison.Ordinal));

    private static bool MatchesPattern(string path, string pattern)
    {
        // Simple glob: * matches any sequence
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(path, regex);
    }
}

public enum FormFillStrategy { Skip, FillWithTestData }
```

- [ ] **Step 5: Run tests, commit**

```bash
dotnet test Iaet.slnx -v n
git add src/Iaet.Core/Models/CrawlReport.cs src/Iaet.Crawler/ tests/Iaet.Crawler.Tests/
git commit -m "feat: add CrawlReport model and CrawlOptions with boundary rules"
```

---

## Task 2: ElementDiscoverer (TDD)

Discovers interactive elements on a page. Uses Playwright's `IPage.QuerySelectorAllAsync` to find links, buttons, forms, and other clickable elements.

**Files:**
- Create: `src/Iaet.Crawler/ElementDiscoverer.cs`
- Create: `tests/Iaet.Crawler.Tests/ElementDiscovererTests.cs`

- [ ] **Step 1: Write tests**

Since we can't spin up a real browser in unit tests, `ElementDiscoverer` accepts an `IElementQueryable` abstraction that wraps Playwright's page queries. Tests use mocks.

```csharp
public class ElementDiscovererTests
{
    [Fact]
    public async Task DiscoverAsync_FindsLinks()
    {
        var queryable = Substitute.For<IElementQueryable>();
        queryable.QuerySelectorAllAsync("a[href]", Arg.Any<CancellationToken>())
            .Returns([new ElementInfo("a", "a[href='page2']", "Page 2", "/page2")]);

        var discoverer = new ElementDiscoverer();
        var elements = await discoverer.DiscoverAsync(queryable);

        elements.Should().ContainSingle().Which.TagName.Should().Be("a");
    }

    [Fact]
    public async Task DiscoverAsync_FindsButtons()
    {
        var queryable = Substitute.For<IElementQueryable>();
        queryable.QuerySelectorAllAsync("button:not([disabled])", Arg.Any<CancellationToken>())
            .Returns([new ElementInfo("button", "button.submit", "Submit", null)]);
        queryable.QuerySelectorAllAsync(Arg.Is<string>(s => s != "button:not([disabled])"), Arg.Any<CancellationToken>())
            .Returns([]);

        var discoverer = new ElementDiscoverer();
        var elements = await discoverer.DiscoverAsync(queryable);

        elements.Should().Contain(e => e.TagName == "button");
    }

    [Fact]
    public async Task DiscoverAsync_ExcludesMatchingSelectors()
    {
        var queryable = Substitute.For<IElementQueryable>();
        queryable.QuerySelectorAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([
                new ElementInfo("button", ".save-btn", "Save", null),
                new ElementInfo("button", ".delete-btn", "Delete", null)
            ]);

        var options = new CrawlOptions
        {
            StartUrl = "https://example.com",
            ExcludedSelectors = [".delete-btn"]
        };
        var discoverer = new ElementDiscoverer();
        var elements = await discoverer.DiscoverAsync(queryable, options);

        elements.Should().ContainSingle().Which.Selector.Should().Be(".save-btn");
    }
}
```

- [ ] **Step 2: Create IElementQueryable abstraction and implement**

```csharp
// Abstraction for testability
public interface IElementQueryable
{
    Task<IReadOnlyList<ElementInfo>> QuerySelectorAllAsync(string selector, CancellationToken ct = default);
}

public sealed record ElementInfo(string TagName, string Selector, string? Text, string? Href);

// Playwright implementation
public sealed class PlaywrightElementQueryable : IElementQueryable
{
    private readonly IPage _page;

    public PlaywrightElementQueryable(IPage page) { _page = page; }

    public async Task<IReadOnlyList<ElementInfo>> QuerySelectorAllAsync(string selector, CancellationToken ct)
    {
        var elements = await _page.QuerySelectorAllAsync(selector);
        var result = new List<ElementInfo>();
        foreach (var el in elements)
        {
            var tagName = await el.EvaluateAsync<string>("e => e.tagName.toLowerCase()");
            var text = await el.InnerTextAsync();
            var href = await el.GetAttributeAsync("href");
            var selectorStr = await el.EvaluateAsync<string>(
                "e => e.id ? '#' + e.id : (e.className ? e.tagName.toLowerCase() + '.' + e.className.split(' ')[0] : e.tagName.toLowerCase())");
            result.Add(new ElementInfo(tagName, selectorStr, text?.Trim(), href));
        }
        return result;
    }
}
```

```csharp
// ElementDiscoverer queries for links, buttons, forms, clickable elements
public sealed class ElementDiscoverer
{
    private static readonly string[] DefaultSelectors =
    [
        "a[href]",
        "button:not([disabled])",
        "[role='button']",
        "input[type='submit']",
        "[onclick]"
    ];

    public async Task<IReadOnlyList<DiscoveredElement>> DiscoverAsync(
        IElementQueryable page, CrawlOptions? options = null, CancellationToken ct = default)
    {
        var allElements = new List<DiscoveredElement>();

        foreach (var selector in DefaultSelectors)
        {
            var elements = await page.QuerySelectorAllAsync(selector, ct);
            foreach (var el in elements)
            {
                if (options is not null && options.IsSelectorExcluded(el.Selector))
                    continue;

                allElements.Add(new DiscoveredElement
                {
                    TagName = el.TagName,
                    Selector = el.Selector,
                    Text = el.Text,
                    Href = el.Href,
                    WasInteracted = false
                });
            }
        }

        return allElements;
    }
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test tests/Iaet.Crawler.Tests -v n
git add src/Iaet.Crawler/ tests/Iaet.Crawler.Tests/
git commit -m "feat: add ElementDiscoverer with DOM element discovery and excluded selector filtering"
```

---

## Task 3: PageInteractor — Click/Fill/Navigate (TDD)

Interacts with discovered elements: clicks buttons, follows links, optionally fills forms. Tracks URL changes for SPA support.

**Files:**
- Create: `src/Iaet.Crawler/PageInteractor.cs`
- Create: `tests/Iaet.Crawler.Tests/PageInteractorTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class PageInteractorTests
{
    [Fact]
    public async Task InteractAsync_ClicksLinkElement()
    {
        var navigator = Substitute.For<IPageNavigator>();
        navigator.CurrentUrl.Returns("https://example.com");
        var interactor = new PageInteractor(navigator);

        var element = new DiscoveredElement
        {
            TagName = "a", Selector = "a.link", Text = "Page 2",
            Href = "/page2", WasInteracted = false
        };

        var result = await interactor.InteractAsync(element);
        result.NavigatedTo.Should().Be("https://example.com/page2");
    }

    [Fact]
    public async Task InteractAsync_ClicksButton_TracksUrlChange()
    {
        var navigator = Substitute.For<IPageNavigator>();
        navigator.CurrentUrl.Returns("https://example.com");
        navigator.ClickAndWaitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://example.com/dashboard");

        var interactor = new PageInteractor(navigator);
        var element = new DiscoveredElement
        {
            TagName = "button", Selector = "button.nav", Text = "Dashboard",
            Href = null, WasInteracted = false
        };

        var result = await interactor.InteractAsync(element);
        result.NavigatedTo.Should().Be("https://example.com/dashboard");
    }

    [Fact]
    public async Task InteractAsync_Button_NoNavigation()
    {
        var navigator = Substitute.For<IPageNavigator>();
        navigator.CurrentUrl.Returns("https://example.com");
        navigator.ClickAndWaitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://example.com"); // Same URL — no navigation

        var interactor = new PageInteractor(navigator);
        var element = new DiscoveredElement
        {
            TagName = "button", Selector = "button.action", Text = "Save",
            Href = null, WasInteracted = false
        };

        var result = await interactor.InteractAsync(element);
        result.NavigatedTo.Should().BeNull();
    }
}
```

- [ ] **Step 2: Implement PageInteractor**

```csharp
namespace Iaet.Crawler;

public sealed record InteractionResult(string? NavigatedTo, bool UrlChanged);

public sealed class PageInteractor
{
    private readonly IPageNavigator _navigator;

    public PageInteractor(IPageNavigator navigator) { _navigator = navigator; }

    public async Task<InteractionResult> InteractAsync(
        DiscoveredElement element, CancellationToken ct = default)
    {
        // Links with href: resolve URL, report as navigation target
        if (element.Href is not null)
        {
            var targetUrl = ResolveUrl(_navigator.CurrentUrl, element.Href);
            return new InteractionResult(targetUrl, true);
        }

        // Buttons/other: click and wait for potential SPA navigation
        var urlBefore = _navigator.CurrentUrl;
        var urlAfter = await _navigator.ClickAndWaitAsync(element.Selector, ct);
        var navigated = !string.Equals(urlBefore, urlAfter, StringComparison.OrdinalIgnoreCase);

        return new InteractionResult(
            navigated ? urlAfter : null,
            navigated);
    }

    private static string ResolveUrl(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var abs)) return abs.ToString();
        return new Uri(new Uri(baseUrl), href).ToString();
    }
}
```

- [ ] **Step 3: Expand IPageNavigator with ClickAndWaitAsync**

Add to the `IPageNavigator` interface:
```csharp
/// <summary>Clicks an element and waits for potential navigation. Returns the URL after click.</summary>
Task<string> ClickAndWaitAsync(string selector, CancellationToken ct = default);
```

- [ ] **Step 4: Run tests, commit**

```bash
dotnet test tests/Iaet.Crawler.Tests -v n
git add src/Iaet.Crawler/ tests/Iaet.Crawler.Tests/
git commit -m "feat: add PageInteractor for element interaction with SPA navigation tracking"
```

---

## Task 4: CrawlEngine — Orchestrator (TDD)

The core crawler that ties discovery, interaction, and capture together.

**Files:**
- Create: `src/Iaet.Crawler/CrawlEngine.cs`
- Create: `tests/Iaet.Crawler.Tests/CrawlEngineTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class CrawlEngineTests
{
    [Fact]
    public async Task CrawlAsync_VisitsStartPage()
    {
        var engine = CreateEngine(pages: [
            new FakePage("https://example.com", [])
        ]);

        var report = await engine.CrawlAsync(new CrawlOptions
        {
            StartUrl = "https://example.com",
            MaxPages = 1
        });

        report.Pages.Should().ContainSingle().Which.Url.Should().Be("https://example.com");
    }

    [Fact]
    public async Task CrawlAsync_FollowsLinks()
    {
        var engine = CreateEngine(pages: [
            new FakePage("https://example.com", [
                new ElementInfo("a", "a.link", "Page 2", "/page2")
            ]),
            new FakePage("https://example.com/page2", [])
        ]);

        var report = await engine.CrawlAsync(new CrawlOptions
        {
            StartUrl = "https://example.com",
            MaxPages = 10, MaxDepth = 2
        });

        report.Pages.Should().HaveCount(2);
    }

    [Fact]
    public async Task CrawlAsync_RespectsMaxDepth()
    {
        var engine = CreateEngine(pages: [
            new FakePage("https://example.com", [
                new ElementInfo("a", "a", "P1", "/p1")
            ]),
            new FakePage("https://example.com/p1", [
                new ElementInfo("a", "a", "P2", "/p2")
            ]),
            new FakePage("https://example.com/p2", [])
        ]);

        var report = await engine.CrawlAsync(new CrawlOptions
        {
            StartUrl = "https://example.com",
            MaxDepth = 1 // Only start + 1 level deep
        });

        report.Pages.Should().HaveCount(2); // Start + p1, not p2
    }

    [Fact]
    public async Task CrawlAsync_RespectsMaxPages()
    {
        var engine = CreateEngine(pages: [
            new FakePage("https://example.com", [
                new ElementInfo("a", "a1", "P1", "/p1"),
                new ElementInfo("a", "a2", "P2", "/p2"),
                new ElementInfo("a", "a3", "P3", "/p3")
            ]),
            new FakePage("https://example.com/p1", []),
            new FakePage("https://example.com/p2", []),
            new FakePage("https://example.com/p3", [])
        ]);

        var report = await engine.CrawlAsync(new CrawlOptions
        {
            StartUrl = "https://example.com",
            MaxPages = 2
        });

        report.Pages.Should().HaveCount(2);
    }

    [Fact]
    public async Task CrawlAsync_RespectsUrlBlacklist()
    {
        var engine = CreateEngine(pages: [
            new FakePage("https://example.com", [
                new ElementInfo("a", "a", "Logout", "/logout"),
                new ElementInfo("a", "a", "Settings", "/settings")
            ]),
            new FakePage("https://example.com/settings", [])
        ]);

        var report = await engine.CrawlAsync(new CrawlOptions
        {
            StartUrl = "https://example.com",
            UrlBlacklistPatterns = ["/logout"]
        });

        report.Pages.Select(p => p.Url).Should().NotContain("https://example.com/logout");
    }

    [Fact]
    public async Task CrawlAsync_DoesNotRevisitPages()
    {
        var engine = CreateEngine(pages: [
            new FakePage("https://example.com", [
                new ElementInfo("a", "a", "Self", "/"),
                new ElementInfo("a", "a", "Self2", "/")
            ])
        ]);

        var report = await engine.CrawlAsync(new CrawlOptions
        {
            StartUrl = "https://example.com"
        });

        report.Pages.Should().ContainSingle(); // No duplicate visits
    }
}
```

- [ ] **Step 2: Implement CrawlEngine**

The `CrawlEngine` takes an `IPageNavigator` abstraction (wraps Playwright browser navigation + element query) and an `ElementDiscoverer`.

```csharp
public interface IPageNavigator
{
    Task<IElementQueryable> NavigateAsync(string url, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetApiCallsSinceLastNavigationAsync(CancellationToken ct = default);
    string CurrentUrl { get; }
}

public sealed class CrawlEngine
{
    private readonly IPageNavigator _navigator;
    private readonly ElementDiscoverer _discoverer;

    public CrawlEngine(IPageNavigator navigator, ElementDiscoverer discoverer) { ... }

    public async Task<CrawlReport> CrawlAsync(CrawlOptions options, CancellationToken ct = default)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Url, int Depth)>();
        var pages = new List<DiscoveredPage>();
        var startedAt = DateTimeOffset.UtcNow;

        queue.Enqueue((options.StartUrl, 0));

        while (queue.Count > 0 && pages.Count < options.MaxPages)
        {
            ct.ThrowIfCancellationRequested();

            // Check duration limit
            if ((DateTimeOffset.UtcNow - startedAt).TotalSeconds > options.MaxDurationSeconds)
                break;

            var (url, depth) = queue.Dequeue();
            if (!visited.Add(url)) continue;
            if (!options.IsUrlAllowed(url)) continue;

            var pageQueryable = await _navigator.NavigateAsync(url, ct);
            var elements = await _discoverer.DiscoverAsync(pageQueryable, options, ct);
            var apiCalls = await _navigator.GetApiCallsSinceLastNavigationAsync(ct);

            var navigatedTo = new List<string>();
            foreach (var el in elements.Where(e => e.Href is not null))
            {
                var targetUrl = ResolveUrl(url, el.Href!);
                if (depth + 1 <= options.MaxDepth && !visited.Contains(targetUrl))
                {
                    queue.Enqueue((targetUrl, depth + 1));
                    navigatedTo.Add(targetUrl);
                }
            }

            pages.Add(new DiscoveredPage
            {
                Url = url,
                Depth = depth,
                InteractiveElements = elements,
                ApiCallsTriggered = apiCalls.ToList(),
                NavigatedTo = navigatedTo
            });
        }

        return new CrawlReport
        {
            SessionId = Guid.NewGuid(),
            TargetApplication = options.TargetApplication,
            StartUrl = options.StartUrl,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Pages = pages,
            TotalRequestsCaptured = pages.Sum(p => p.ApiCallsTriggered.Count),
            TotalStreamsCaptured = 0
        };
    }

    private static string ResolveUrl(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var abs)) return abs.ToString();
        return new Uri(new Uri(baseUrl), href).ToString();
    }
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test tests/Iaet.Crawler.Tests -v n
git add src/Iaet.Crawler/ tests/Iaet.Crawler.Tests/
git commit -m "feat: add CrawlEngine with BFS page traversal, boundary rules, and depth limiting"
```

---

## Task 5: RecipeRunner (TDD)

Executes TypeScript Playwright recipes by spawning a Node.js subprocess.

**Files:**
- Create: `src/Iaet.Crawler/RecipeRunner.cs`
- Create: `tests/Iaet.Crawler.Tests/RecipeRunnerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class RecipeRunnerTests
{
    [Fact]
    public void Validate_MissingFile_ThrowsFileNotFound()
    {
        var runner = new RecipeRunner();
        var act = () => runner.ValidateRecipe("/nonexistent/recipe.ts");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Validate_NonTsFile_ThrowsArgument()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var runner = new RecipeRunner();
            var act = () => runner.ValidateRecipe(tempFile);
            act.Should().Throw<ArgumentException>().WithMessage("*TypeScript*");
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void Validate_ValidTsFile_DoesNotThrow()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test-recipe.ts");
        File.WriteAllText(tempFile, "// test recipe");
        try
        {
            var runner = new RecipeRunner();
            var act = () => runner.ValidateRecipe(tempFile);
            act.Should().NotThrow();
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task BuildCommandAsync_IncludesCorrectArgs()
    {
        var runner = new RecipeRunner();
        var (command, args) = runner.BuildCommand("/path/to/recipe.ts", 9222);

        command.Should().Be("npx");
        args.Should().Contain("playwright");
        args.Should().Contain("test");
        args.Should().Contain("/path/to/recipe.ts");
    }
}
```

- [ ] **Step 2: Implement RecipeRunner**

```csharp
using System.Diagnostics;

namespace Iaet.Crawler;

public sealed class RecipeRunner
{
    public void ValidateRecipe(string recipePath)
    {
        if (!File.Exists(recipePath))
            throw new FileNotFoundException($"Recipe not found: {recipePath}", recipePath);
        if (!recipePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Recipe must be a TypeScript (.ts) file", nameof(recipePath));
    }

    public (string Command, string Args) BuildCommand(string recipePath, int cdpPort)
    {
        // npx tsx executes TS files directly — recipes use chromium.connectOverCDP() internally
        // CDP_ENDPOINT env var is set so the recipe can connect to IAET's browser
        return ("npx", $"tsx \"{recipePath}\"");
    }

    /// <summary>Environment variables to pass to the recipe subprocess.</summary>
    public Dictionary<string, string> GetEnvironment(int cdpPort) => new()
    {
        ["CDP_ENDPOINT"] = $"ws://127.0.0.1:{cdpPort}"
    };

    public async Task<int> RunAsync(string recipePath, int cdpPort, CancellationToken ct = default)
    {
        ValidateRecipe(recipePath);
        var (command, args) = BuildCommand(recipePath, cdpPort);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        process.Start();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test tests/Iaet.Crawler.Tests -v n
git add src/Iaet.Crawler/ tests/Iaet.Crawler.Tests/
git commit -m "feat: add RecipeRunner for TypeScript Playwright recipe execution"
```

---

## Task 6: DI + CLI Commands

**Files:**
- Create: `src/Iaet.Crawler/ServiceCollectionExtensions.cs`
- Create: `src/Iaet.Cli/Commands/CrawlCommand.cs`
- Modify: `src/Iaet.Cli/Commands/CaptureCommand.cs`
- Modify: `src/Iaet.Cli/Program.cs`

- [ ] **Step 1: Create DI registration**

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCrawler(this IServiceCollection services)
    {
        services.AddTransient<ElementDiscoverer>();
        services.AddTransient<RecipeRunner>();
        return services;
    }
}
```

- [ ] **Step 2: Create CrawlCommand**

```bash
dotnet add src/Iaet.Cli reference src/Iaet.Crawler
```

`iaet crawl` with options:
- `--url <string>` (required) — starting URL
- `--target <string>` — target app name
- `--max-depth <int>` (default 3)
- `--max-pages <int>` (default 50)
- `--max-duration <int>` (default 300 seconds)
- `--headless` flag
- `--blacklist <string[]>` — URL path patterns to avoid
- `--exclude-selector <string[]>` — CSS selectors to skip
- `--session <string>` — session name
- `--db <string>` — database path (override DI default)

The command:
1. Creates CrawlOptions from CLI args
2. Creates a real `PlaywrightPageNavigator` (wraps PlaywrightCaptureSession + ElementDiscoverer)
3. Runs CrawlEngine.CrawlAsync
4. Saves CrawlReport as JSON to output file
5. Prints summary

- [ ] **Step 3: Add `--recipe` to CaptureCommand**

Add a `run` subcommand to `capture`:
- `iaet capture run --recipe <path> --session <name> --db <path>`
- Creates PlaywrightCaptureSession with CDP port exposed
- Runs RecipeRunner.RunAsync with that port
- Drains captured requests to catalog

- [ ] **Step 4: Register in Program.cs**

```csharp
services.AddIaetCrawler();
// ...
CrawlCommand.Create(host.Services)
```

- [ ] **Step 5: Verify**

```bash
dotnet run --project src/Iaet.Cli -- crawl --help
dotnet run --project src/Iaet.Cli -- capture run --help
```

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Crawler/ src/Iaet.Cli/
git commit -m "feat: add crawl CLI command and capture run --recipe support"
```

---

## Task 7: Example Recipes + Docs + PR

- [ ] **Step 1: Create example TypeScript recipes**

`docs/recipes/spotify-playlist-capture.ts`:
```typescript
import { chromium } from 'playwright';

// Connect to IAET's browser instance
const browser = await chromium.connectOverCDP(process.env.CDP_ENDPOINT || 'ws://127.0.0.1:9222');
const context = browser.contexts()[0];
const page = context.pages()[0];

// Navigate and interact
await page.goto('https://open.spotify.com');
await page.waitForTimeout(3000);

// Search for a playlist
await page.fill('[data-testid="search-input"]', 'Top 50');
await page.waitForTimeout(2000);

// Click first result
await page.click('[data-testid="search-result"]:first-child');
await page.waitForTimeout(3000);

// Tag this interaction
await page.evaluate(() => (window as any).__iaet_tag = 'playlist-view');

// Play a track
await page.click('[data-testid="play-button"]');
await page.waitForTimeout(5000);

await browser.close();
```

`docs/recipes/github-api-discovery.ts`:
```typescript
import { chromium } from 'playwright';

const browser = await chromium.connectOverCDP(process.env.CDP_ENDPOINT || 'ws://127.0.0.1:9222');
const context = browser.contexts()[0];
const page = context.pages()[0];

await page.goto('https://github.com');
await page.waitForTimeout(2000);

// Navigate to a repo
await page.fill('[name="q"]', 'playwright');
await page.press('[name="q"]', 'Enter');
await page.waitForTimeout(3000);

// Click first result
await page.click('.repo-list-item a:first-child');
await page.waitForTimeout(3000);

// Browse tabs
for (const tab of ['issues', 'pulls', 'actions']) {
    await page.click(`[data-tab="${tab}"]`);
    await page.waitForTimeout(2000);
}

await browser.close();
```

- [ ] **Step 2: Update README and Crawler README**

- Move Crawler from "coming" to implemented in README features
- Add crawl usage examples
- Update CLI reference tree
- Write `src/Iaet.Crawler/README.md`

- [ ] **Step 3: Run full test suite**

```bash
dotnet test Iaet.slnx -c Release
```

- [ ] **Step 4: Push and create PR**

```bash
git add docs/recipes/ README.md src/Iaet.Crawler/README.md
git commit -m "docs: add example recipes and crawler documentation"
git push origin phase5-crawler
```

```bash
gh pr create --title "Phase 5: Semi-Autonomous Crawler + Recipe Runner" --body "$(cat <<'EOF'
## Summary

Adds semi-autonomous web crawling and TypeScript recipe support to IAET:

- **CrawlReport model** — discovered pages, interactive elements, API calls per page
- **CrawlOptions** — boundary rules: URL whitelist/blacklist, max depth/pages/duration, excluded selectors, form strategy
- **ElementDiscoverer** — DOM inspection for interactive elements (links, buttons, forms)
- **CrawlEngine** — BFS page traversal with boundary enforcement and API capture
- **RecipeRunner** — TypeScript Playwright recipe execution via Node.js subprocess
- **CLI commands** — `iaet crawl` with full boundary options, `iaet capture run --recipe`
- **Example recipes** — Spotify playlist capture, GitHub API discovery

## Test Plan
- [ ] CrawlOptions: defaults, URL whitelist/blacklist, excluded selectors
- [ ] ElementDiscoverer: finds links, buttons, excludes selectors
- [ ] CrawlEngine: visits start page, follows links, respects depth/pages/blacklist, no revisits
- [ ] RecipeRunner: validates file, builds command, handles missing/wrong file types
- [ ] CLI: crawl --help, capture run --help

Generated with Claude Code
EOF
)"
```

---

## What's Next

After Phase 5, IAET has:
- Capture (HTTP + streams) — Phases 1-2
- Analysis (schema + replay) — Phase 3
- Export (6 formats) — Phase 4
- Automated discovery (crawler + recipes) — Phase 5

**Phase 6 (Explorer)** adds a local Swagger-like web UI for browsing captured data.
