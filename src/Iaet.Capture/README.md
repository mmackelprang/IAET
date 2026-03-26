# Iaet.Capture

`Iaet.Capture` is the browser-capture layer. It uses [Microsoft Playwright](https://playwright.dev/dotnet/) to launch a Chromium browser and attaches a Chrome DevTools Protocol network listener to intercept HTTP traffic produced by user interactions.

## PlaywrightCaptureSession

`PlaywrightCaptureSession` implements `ICaptureSession`. Calling `StartAsync(url)` launches Chromium (headed or headless), navigates to the starting URL, and begins recording. `GetCapturedRequestsAsync()` drains all buffered requests after you call `StopAsync()`. The session is `IAsyncDisposable`; the CLI wraps it in `await using` to guarantee the browser is closed even if the user presses Ctrl-C.

## CaptureOptions

```csharp
public sealed class CaptureOptions
{
    public required string TargetApplication { get; init; }
    public string? Profile { get; init; }   // Chromium profile directory name
    public bool Headless { get; init; }
}
```

`Profile` is forwarded to Chromium as `--profile-directory`, which lets you reuse a logged-in browser profile so the target app does not need you to re-authenticate before capturing.

## Factory Pattern

`ICaptureSessionFactory` / `PlaywrightCaptureSessionFactory` follows the factory pattern so `Iaet.Cli` can resolve a new session per command invocation without taking a hard dependency on `PlaywrightCaptureSession`. Register via the extension method:

```csharp
services.AddIaetCapture();
```

## RequestSanitizer

`RequestSanitizer.SanitizeHeaders` strips credential-bearing headers before any request is handed to the catalog. The redacted set is: `Authorization`, `Cookie`, `Set-Cookie`, `X-CSRF-Token`, `X-XSRF-Token`, `X-Goog-AuthUser`. The value is replaced with the literal string `<REDACTED>`. The set is internal and cannot be shrunk at runtime — this is intentional to prevent accidental credential leakage.
