# Iaet.Crawler

`Iaet.Crawler` provides a semi-autonomous browser crawler that drives Playwright through a target application's UI, triggering API calls automatically to expand capture coverage beyond manual interaction.

---

## Components

### CrawlOptions

Configuration for a crawl run. All members have safe defaults.

| Property | Default | Description |
|---|---|---|
| `StartUrl` | *(required)* | Starting URL for the BFS traversal |
| `TargetApplication` | `"Unknown"` | Label stored on the resulting report |
| `MaxDepth` | `3` | Maximum link-follow depth |
| `MaxPages` | `50` | Maximum number of pages to visit |
| `MaxDurationSeconds` | `300` | Hard timeout; crawl stops after this many seconds |
| `Headless` | `false` | Pass through to Playwright browser launch |
| `UrlWhitelistPatterns` | `[]` | If non-empty, only matching URL paths are visited |
| `UrlBlacklistPatterns` | `[]` | URL path patterns that are always skipped |
| `ExcludedSelectors` | `[]` | CSS selectors whose elements are ignored during discovery |
| `FormStrategy` | `Skip` | How to handle `<form>` elements (`Skip` or `FillWithTestData`) |

Patterns support `*` wildcards (e.g. `/api/*`, `/logout`).

---

### ElementDiscoverer

Queries the current page for interactive elements using a fixed set of CSS selectors:

- `a[href]`
- `button:not([disabled])`
- `[role='button']`
- `input[type='submit']`
- `[onclick]`

Elements whose selectors match any entry in `CrawlOptions.ExcludedSelectors` are skipped. Duplicate selectors within one page are deduplicated.

```csharp
var discoverer = new ElementDiscoverer(options);
var elements = await discoverer.DiscoverAsync(queryable, ct);
```

---

### PageInteractor

Interacts with a single `DiscoveredElement` and reports whether navigation occurred.

- For `<a>` elements — resolves the `href` relative to the current URL.
- For buttons and other clickable elements — clicks the element and checks whether the URL changed afterward (SPA navigation tracking).

```csharp
var interactor = new PageInteractor(navigator);
InteractionResult result = await interactor.InteractAsync(element, ct);
// result.NavigatedTo is non-null when the click triggered navigation
```

---

### CrawlEngine

Orchestrates a full BFS crawl using `ElementDiscoverer` and `PageInteractor`.

```csharp
var engine = new CrawlEngine(options, navigator);
CrawlReport report = await engine.RunAsync(ct);
```

Boundary rules enforced:
- Visits at most `MaxPages` pages.
- Respects `MaxDepth` — links discovered at depth N are enqueued at depth N+1 and ignored once N+1 > MaxDepth.
- Stops after `MaxDurationSeconds` regardless of queue state.
- Never revisits a URL (case-insensitive URL set).
- Skips URLs blocked by `IsUrlAllowed` (whitelist/blacklist).

The `IPageNavigator` dependency must be supplied by the caller. A real implementation (`PlaywrightPageNavigator`) lives in `Iaet.Capture` and bridges to a live Playwright browser.

---

### RecipeRunner

Executes a TypeScript Playwright recipe by spawning `npx tsx <recipe>` with `CDP_ENDPOINT` set to `ws://127.0.0.1:<port>`.

```csharp
// Validate before running
RecipeRunner.ValidateRecipe("scripts/my-recipe.ts");

// Build the shell command for display / dry-run
var (cmd, args) = RecipeRunner.BuildCommand("scripts/my-recipe.ts", 9222);
// cmd  = "npx"
// args = "tsx \"scripts/my-recipe.ts\""

// Run and return exit code
int code = await RecipeRunner.RunAsync("scripts/my-recipe.ts", 9222, ct);
```

`ValidateRecipe` throws `FileNotFoundException` if the file does not exist and `ArgumentException` if the path does not end in `.ts`.

Example recipes are in `docs/recipes/`.

---

## DI Registration

```csharp
services.AddIaetCrawler();
```

Registers `ElementDiscoverer` and `RecipeRunner` as transient services.
