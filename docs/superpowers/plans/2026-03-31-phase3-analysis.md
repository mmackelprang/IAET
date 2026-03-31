# Phase 3: Analysis Components Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add JS bundle static analysis, protocol-aware stream analysis, and dependency/auth-chain detection to the IAET toolkit.

**Architecture:** Two new assemblies (`Iaet.JsAnalysis` for JavaScript bundle URL/pattern extraction, `Iaet.ProtocolAnalysis` for stream protocol analysis with pluggable `IStreamAnalyzer` contract) plus extensions to `Iaet.Schema` for request dependency ordering, auth chain detection, and non-JSON body robustness. All analysis is offline — operates on previously captured data, no live browser required.

**Tech Stack:** .NET 10, regex-based JS pattern matching (no Node.js dependency for v1), System.Text.Json, xUnit + FluentAssertions + NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-31-agent-investigation-team-design.md` (Phase 3 section)

---

## File Structure

### New assembly: Iaet.JsAnalysis

```
src/Iaet.JsAnalysis/
  Iaet.JsAnalysis.csproj
  BundleDownloader.cs           — fetch JS bundles from URLs
  UrlExtractor.cs               — regex-based URL pattern discovery
  FetchCallExtractor.cs         — find fetch()/XHR call sites
  GraphQlExtractor.cs           — find GraphQL queries/mutations
  ConfigExtractor.cs            — find config objects, feature flags
  WebSocketUrlExtractor.cs      — find new WebSocket(url) patterns
  BundleAnalysisResult.cs       — aggregated analysis output
  ServiceCollectionExtensions.cs
tests/Iaet.JsAnalysis.Tests/
  Iaet.JsAnalysis.Tests.csproj
  UrlExtractorTests.cs
  FetchCallExtractorTests.cs
  GraphQlExtractorTests.cs
  ConfigExtractorTests.cs
  WebSocketUrlExtractorTests.cs
```

### New assembly: Iaet.ProtocolAnalysis

```
src/Iaet.ProtocolAnalysis/
  Iaet.ProtocolAnalysis.csproj
  IStreamAnalyzer.cs            — pluggable analysis contract
  StreamAnalysis.cs             — analysis result model
  WebSocketAnalyzer.cs          — message type classification
  SdpParser.cs                  — SDP offer/answer parsing
  GrpcServiceExtractor.cs      — service/method from gRPC-Web
  MediaManifestAnalyzer.cs      — HLS/DASH manifest parsing
  StateMachineBuilder.cs        — observed transitions → state model
  ServiceCollectionExtensions.cs
tests/Iaet.ProtocolAnalysis.Tests/
  Iaet.ProtocolAnalysis.Tests.csproj
  WebSocketAnalyzerTests.cs
  SdpParserTests.cs
  GrpcServiceExtractorTests.cs
  MediaManifestAnalyzerTests.cs
  StateMachineBuilderTests.cs
```

### Extensions to Iaet.Schema

```
src/Iaet.Schema/
  DependencyGraphBuilder.cs     — request ordering constraints
  AuthChainDetector.cs          — cookie → token → API chains
  SharedIdTracer.cs             — IDs appearing across endpoints
  RateLimitDetector.cs          — 429 pattern detection
src/Iaet.Schema/
  JsonTypeMap.cs                — (modify) add non-JSON robustness
tests/Iaet.Schema.Tests/
  DependencyGraphBuilderTests.cs
  AuthChainDetectorTests.cs
  SharedIdTracerTests.cs
  RateLimitDetectorTests.cs
  JsonTypeMapRobustnessTests.cs
```

### New models in Iaet.Core

```
src/Iaet.Core/Models/
  ExtractedUrl.cs               — URL found in JS with source context
  RequestDependency.cs          — ordering constraint between endpoints
  AuthChain.cs                  — cookie → token → API chain
  StreamAnalysisResult.cs       — protocol analysis output
  StateMachineModel.cs          — states + transitions
```

---

## Task 1: Solution Scaffolding

**Files:**
- Create: `src/Iaet.JsAnalysis/Iaet.JsAnalysis.csproj`
- Create: `src/Iaet.ProtocolAnalysis/Iaet.ProtocolAnalysis.csproj`
- Create: `tests/Iaet.JsAnalysis.Tests/Iaet.JsAnalysis.Tests.csproj`
- Create: `tests/Iaet.ProtocolAnalysis.Tests/Iaet.ProtocolAnalysis.Tests.csproj`
- Modify: `Iaet.slnx`

- [ ] **Step 1: Create project files**

```xml
<!-- src/Iaet.JsAnalysis/Iaet.JsAnalysis.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

```xml
<!-- src/Iaet.ProtocolAnalysis/Iaet.ProtocolAnalysis.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create test project files**

```xml
<!-- tests/Iaet.JsAnalysis.Tests/Iaet.JsAnalysis.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.9.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Iaet.JsAnalysis\Iaet.JsAnalysis.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/Iaet.ProtocolAnalysis.Tests/Iaet.ProtocolAnalysis.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.9.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Iaet.ProtocolAnalysis\Iaet.ProtocolAnalysis.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Update Iaet.slnx**

Add 4 entries: 2 in `/src/`, 2 in `/tests/`.

- [ ] **Step 4: Verify build and commit**

Run: `dotnet build Iaet.slnx`

```bash
git add src/Iaet.JsAnalysis/ src/Iaet.ProtocolAnalysis/ tests/Iaet.JsAnalysis.Tests/ tests/Iaet.ProtocolAnalysis.Tests/ Iaet.slnx
git commit -m "chore: scaffold Iaet.JsAnalysis and Iaet.ProtocolAnalysis assemblies"
```

---

## Task 2: Analysis Domain Models

**Files:**
- Create: `src/Iaet.Core/Models/ExtractedUrl.cs`
- Create: `src/Iaet.Core/Models/RequestDependency.cs`
- Create: `src/Iaet.Core/Models/AuthChain.cs`
- Create: `src/Iaet.Core/Models/StreamAnalysisResult.cs`
- Create: `src/Iaet.Core/Models/StateMachineModel.cs`
- Test: `tests/Iaet.Core.Tests/Models/AnalysisModelTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Core.Tests/Models/AnalysisModelTests.cs
using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class AnalysisModelTests
{
    [Fact]
    public void ExtractedUrl_holds_url_with_source_context()
    {
        var url = new ExtractedUrl
        {
            Url = "/api/v1/users",
            HttpMethod = "GET",
            SourceFile = "main-bundle.js",
            LineNumber = 4521,
            Confidence = ConfidenceLevel.High,
            Context = "fetch(\"/api/v1/users\")",
        };

        url.Url.Should().Be("/api/v1/users");
        url.SourceFile.Should().Be("main-bundle.js");
    }

    [Fact]
    public void RequestDependency_describes_ordering_constraint()
    {
        var dep = new RequestDependency
        {
            From = "GET /api/session",
            To = "GET /api/calls",
            Reason = "X-Session-Id header required",
            SharedField = "sessionId",
        };

        dep.From.Should().Be("GET /api/session");
        dep.To.Should().Be("GET /api/calls");
    }

    [Fact]
    public void AuthChain_traces_credential_flow()
    {
        var chain = new AuthChain
        {
            Name = "Google Voice session",
            Steps =
            [
                new AuthChainStep { Endpoint = "POST /login", Provides = "session_cookie", Type = "cookie" },
                new AuthChainStep { Endpoint = "GET /api/token", Provides = "access_token", Type = "header" },
                new AuthChainStep { Endpoint = "GET /api/calls", Consumes = "access_token", Type = "header" },
            ],
        };

        chain.Steps.Should().HaveCount(3);
    }

    [Fact]
    public void StreamAnalysisResult_carries_protocol_findings()
    {
        var result = new StreamAnalysisResult
        {
            StreamId = Guid.NewGuid(),
            Protocol = StreamProtocol.WebSocket,
            MessageTypes = ["control", "data", "heartbeat"],
            SubProtocol = "graphql-ws",
            Confidence = ConfidenceLevel.High,
        };

        result.MessageTypes.Should().HaveCount(3);
        result.SubProtocol.Should().Be("graphql-ws");
    }

    [Fact]
    public void StateMachineModel_has_states_and_transitions()
    {
        var sm = new StateMachineModel
        {
            Name = "WebRTC Connection",
            States = ["new", "connecting", "connected", "disconnected"],
            Transitions =
            [
                new StateTransition { From = "new", To = "connecting", Trigger = "createOffer" },
                new StateTransition { From = "connecting", To = "connected", Trigger = "iceComplete" },
            ],
            InitialState = "new",
        };

        sm.States.Should().HaveCount(4);
        sm.Transitions.Should().HaveCount(2);
    }
}
```

- [ ] **Step 2: Create models**

```csharp
// src/Iaet.Core/Models/ExtractedUrl.cs
namespace Iaet.Core.Models;

public sealed record ExtractedUrl
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; init; }
    public string? HttpMethod { get; init; }
    public string? SourceFile { get; init; }
    public int? LineNumber { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
    public string? Context { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/RequestDependency.cs
namespace Iaet.Core.Models;

public sealed record RequestDependency
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Reason { get; init; }
    public string? SharedField { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/AuthChainStep.cs
namespace Iaet.Core.Models;

public sealed record AuthChainStep
{
    public required string Endpoint { get; init; }
    public string? Provides { get; init; }
    public string? Consumes { get; init; }
    public required string Type { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/AuthChain.cs
namespace Iaet.Core.Models;

public sealed record AuthChain
{
    public required string Name { get; init; }
    public required IReadOnlyList<AuthChainStep> Steps { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/StreamAnalysisResult.cs
namespace Iaet.Core.Models;

public sealed record StreamAnalysisResult
{
    public required Guid StreamId { get; init; }
    public required StreamProtocol Protocol { get; init; }
    public IReadOnlyList<string> MessageTypes { get; init; } = [];
    public string? SubProtocol { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public StateMachineModel? StateMachine { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/StateTransition.cs
namespace Iaet.Core.Models;

public sealed record StateTransition
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Trigger { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/StateMachineModel.cs
namespace Iaet.Core.Models;

public sealed record StateMachineModel
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> States { get; init; }
    public required IReadOnlyList<StateTransition> Transitions { get; init; }
    public required string InitialState { get; init; }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Core.Tests/ --filter "FullyQualifiedName~AnalysisModelTests" -v quiet`

```bash
git add src/Iaet.Core/Models/ tests/Iaet.Core.Tests/Models/AnalysisModelTests.cs
git commit -m "feat(core): add analysis domain models (ExtractedUrl, RequestDependency, AuthChain, StreamAnalysis, StateMachine)"
```

---

## Task 3: JS URL Extractor

**Files:**
- Create: `src/Iaet.JsAnalysis/UrlExtractor.cs`
- Test: `tests/Iaet.JsAnalysis.Tests/UrlExtractorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.JsAnalysis.Tests/UrlExtractorTests.cs
using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class UrlExtractorTests
{
    [Fact]
    public void Extract_finds_absolute_api_urls()
    {
        var js = """
            const baseUrl = "https://clients6.google.com/voice/v1/voiceclient";
            fetch("https://voice.google.com/api/v2/calls");
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "https://clients6.google.com/voice/v1/voiceclient");
        urls.Should().Contain(u => u.Url == "https://voice.google.com/api/v2/calls");
    }

    [Fact]
    public void Extract_finds_relative_api_paths()
    {
        var js = """
            fetch("/api/v1/users");
            xhr.open("GET", "/api/v1/messages");
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "/api/v1/users");
        urls.Should().Contain(u => u.Url == "/api/v1/messages");
    }

    [Fact]
    public void Extract_ignores_non_api_paths()
    {
        var js = """
            import "/static/styles.css";
            const img = "/images/logo.png";
            const path = "/api/v1/data";
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().NotContain(u => u.Url.Contains(".css"));
        urls.Should().NotContain(u => u.Url.Contains(".png"));
        urls.Should().Contain(u => u.Url == "/api/v1/data");
    }

    [Fact]
    public void Extract_reports_line_numbers()
    {
        var js = "line1\nfetch(\"/api/test\")\nline3";

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "/api/test" && u.LineNumber == 2);
    }

    [Fact]
    public void Extract_includes_source_context()
    {
        var js = """fetch("/api/v1/users")""";

        var urls = UrlExtractor.Extract(js, "main.js");

        urls.Should().Contain(u => u.SourceFile == "main.js");
    }

    [Fact]
    public void Extract_deduplicates_urls()
    {
        var js = """
            fetch("/api/v1/users");
            fetch("/api/v1/users");
            fetch("/api/v1/users");
            """;

        var urls = UrlExtractor.Extract(js, "bundle.js");

        urls.Where(u => u.Url == "/api/v1/users").Should().HaveCount(1);
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        var urls = UrlExtractor.Extract("", "bundle.js");
        urls.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Implement UrlExtractor**

```csharp
// src/Iaet.JsAnalysis/UrlExtractor.cs
using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public static partial class UrlExtractor
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2",
        ".ttf", ".eot", ".mp3", ".mp4", ".webm", ".webp", ".map",
    };

    public static IReadOnlyList<ExtractedUrl> Extract(string jsContent, string sourceFile)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ExtractedUrl>();
        var lines = jsContent.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            foreach (Match match in UrlPattern().Matches(line))
            {
                var url = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                if (IsIgnoredUrl(url))
                    continue;

                if (!seen.Add(url))
                    continue;

                results.Add(new ExtractedUrl
                {
                    Url = url,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Context = line.Trim().Length > 120 ? line.Trim()[..120] : line.Trim(),
                    Confidence = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? ConfidenceLevel.High
                        : ConfidenceLevel.Medium,
                });
            }
        }

        return results;
    }

    private static bool IsIgnoredUrl(string url)
    {
        if (!url.StartsWith('/') && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var ext in IgnoredExtensions)
        {
            if (url.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    [GeneratedRegex("""["'`]((?:https?://[^\s"'`]+)|(?:/[a-zA-Z][a-zA-Z0-9_/\-.{}*]+))["'`]""")]
    private static partial Regex UrlPattern();
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.JsAnalysis.Tests/ --filter "FullyQualifiedName~UrlExtractorTests" -v quiet`

```bash
git add src/Iaet.JsAnalysis/UrlExtractor.cs tests/Iaet.JsAnalysis.Tests/UrlExtractorTests.cs
git commit -m "feat(js): implement UrlExtractor for API URL discovery in JS bundles"
```

---

## Task 4: FetchCall and WebSocket URL Extractors

**Files:**
- Create: `src/Iaet.JsAnalysis/FetchCallExtractor.cs`
- Create: `src/Iaet.JsAnalysis/WebSocketUrlExtractor.cs`
- Test: `tests/Iaet.JsAnalysis.Tests/FetchCallExtractorTests.cs`
- Test: `tests/Iaet.JsAnalysis.Tests/WebSocketUrlExtractorTests.cs`

- [ ] **Step 1: Write FetchCallExtractor tests**

```csharp
// tests/Iaet.JsAnalysis.Tests/FetchCallExtractorTests.cs
using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class FetchCallExtractorTests
{
    [Fact]
    public void Extract_finds_fetch_calls_with_method()
    {
        var js = """
            fetch("/api/users", { method: "POST", body: JSON.stringify(data) });
            fetch("/api/sessions", { method: "DELETE" });
            """;

        var calls = FetchCallExtractor.Extract(js, "app.js");

        calls.Should().Contain(u => u.Url == "/api/users" && u.HttpMethod == "POST");
        calls.Should().Contain(u => u.Url == "/api/sessions" && u.HttpMethod == "DELETE");
    }

    [Fact]
    public void Extract_defaults_to_GET_when_no_method()
    {
        var js = """fetch("/api/data")""";

        var calls = FetchCallExtractor.Extract(js, "app.js");

        calls.Should().Contain(u => u.Url == "/api/data" && u.HttpMethod == "GET");
    }

    [Fact]
    public void Extract_finds_xhr_open_calls()
    {
        var js = """xhr.open("PUT", "/api/users/123")""";

        var calls = FetchCallExtractor.Extract(js, "app.js");

        calls.Should().Contain(u => u.Url == "/api/users/123" && u.HttpMethod == "PUT");
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        FetchCallExtractor.Extract("", "x.js").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Write WebSocketUrlExtractor tests**

```csharp
// tests/Iaet.JsAnalysis.Tests/WebSocketUrlExtractorTests.cs
using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class WebSocketUrlExtractorTests
{
    [Fact]
    public void Extract_finds_websocket_constructor_urls()
    {
        var js = """
            const ws = new WebSocket("wss://voice.google.com/signal");
            const ws2 = new WebSocket('ws://localhost:8080/ws');
            """;

        var urls = WebSocketUrlExtractor.Extract(js, "bundle.js");

        urls.Should().Contain(u => u.Url == "wss://voice.google.com/signal");
        urls.Should().Contain(u => u.Url == "ws://localhost:8080/ws");
    }

    [Fact]
    public void Extract_handles_no_websockets()
    {
        WebSocketUrlExtractor.Extract("var x = 1;", "bundle.js").Should().BeEmpty();
    }
}
```

- [ ] **Step 3: Implement FetchCallExtractor**

```csharp
// src/Iaet.JsAnalysis/FetchCallExtractor.cs
using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public static partial class FetchCallExtractor
{
    public static IReadOnlyList<ExtractedUrl> Extract(string jsContent, string sourceFile)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<ExtractedUrl>();
        var lines = jsContent.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            foreach (Match match in FetchPattern().Matches(line))
            {
                var url = match.Groups[1].Value;
                var method = "GET";
                var methodMatch = FetchMethodPattern().Match(line);
                if (methodMatch.Success)
                    method = methodMatch.Groups[1].Value.ToUpperInvariant();

                results.Add(new ExtractedUrl
                {
                    Url = url,
                    HttpMethod = method,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                });
            }

            foreach (Match match in XhrOpenPattern().Matches(line))
            {
                results.Add(new ExtractedUrl
                {
                    Url = match.Groups[2].Value,
                    HttpMethod = match.Groups[1].Value.ToUpperInvariant(),
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                });
            }
        }

        return results;
    }

    [GeneratedRegex("""fetch\(["'`]((?:/|https?://)[^"'`]+)["'`]""")]
    private static partial Regex FetchPattern();

    [GeneratedRegex("""method\s*:\s*["'`](\w+)["'`]""")]
    private static partial Regex FetchMethodPattern();

    [GeneratedRegex("""\.open\(["'`](\w+)["'`]\s*,\s*["'`]((?:/|https?://)[^"'`]+)["'`]""")]
    private static partial Regex XhrOpenPattern();
}
```

- [ ] **Step 4: Implement WebSocketUrlExtractor**

```csharp
// src/Iaet.JsAnalysis/WebSocketUrlExtractor.cs
using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public static partial class WebSocketUrlExtractor
{
    public static IReadOnlyList<ExtractedUrl> Extract(string jsContent, string sourceFile)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<ExtractedUrl>();
        var lines = jsContent.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            foreach (Match match in WsConstructorPattern().Matches(lines[lineIdx]))
            {
                results.Add(new ExtractedUrl
                {
                    Url = match.Groups[1].Value,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                    Context = "WebSocket constructor",
                });
            }
        }

        return results;
    }

    [GeneratedRegex("""new\s+WebSocket\(["'`](wss?://[^"'`]+)["'`]""")]
    private static partial Regex WsConstructorPattern();
}
```

- [ ] **Step 5: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.JsAnalysis.Tests/ -v quiet`

```bash
git add src/Iaet.JsAnalysis/ tests/Iaet.JsAnalysis.Tests/
git commit -m "feat(js): implement FetchCallExtractor and WebSocketUrlExtractor"
```

---

## Task 5: GraphQL and Config Extractors

**Files:**
- Create: `src/Iaet.JsAnalysis/GraphQlExtractor.cs`
- Create: `src/Iaet.JsAnalysis/ConfigExtractor.cs`
- Create: `src/Iaet.JsAnalysis/BundleAnalysisResult.cs`
- Create: `src/Iaet.JsAnalysis/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.JsAnalysis.Tests/GraphQlExtractorTests.cs`
- Test: `tests/Iaet.JsAnalysis.Tests/ConfigExtractorTests.cs`

- [ ] **Step 1: Write GraphQlExtractor tests**

```csharp
// tests/Iaet.JsAnalysis.Tests/GraphQlExtractorTests.cs
using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class GraphQlExtractorTests
{
    [Fact]
    public void Extract_finds_query_strings()
    {
        var js = """
            const QUERY = `query GetUser($id: ID!) { user(id: $id) { name email } }`;
            const MUTATION = "mutation CreateUser($input: UserInput!) { createUser(input: $input) { id } }";
            """;

        var queries = GraphQlExtractor.Extract(js);

        queries.Should().Contain(q => q.Contains("GetUser"));
        queries.Should().Contain(q => q.Contains("CreateUser"));
    }

    [Fact]
    public void Extract_handles_no_graphql()
    {
        GraphQlExtractor.Extract("var x = 1;").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Write ConfigExtractor tests**

```csharp
// tests/Iaet.JsAnalysis.Tests/ConfigExtractorTests.cs
using FluentAssertions;
using Iaet.JsAnalysis;

namespace Iaet.JsAnalysis.Tests;

public sealed class ConfigExtractorTests
{
    [Fact]
    public void Extract_finds_api_base_urls()
    {
        var js = """
            const API_BASE = "https://api.example.com/v2";
            const config = { apiUrl: "https://voice.google.com/api", timeout: 5000 };
            """;

        var configs = ConfigExtractor.Extract(js);

        configs.Should().Contain(c => c.Key == "API_BASE" && c.Value == "https://api.example.com/v2");
    }

    [Fact]
    public void Extract_finds_feature_flags()
    {
        var js = """
            const ENABLE_WEBRTC = true;
            const FEATURE_FLAGS = { enableSms: false, enableVoip: true };
            """;

        var configs = ConfigExtractor.Extract(js);

        configs.Should().Contain(c => c.Key == "ENABLE_WEBRTC");
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        ConfigExtractor.Extract("").Should().BeEmpty();
    }
}
```

- [ ] **Step 3: Implement GraphQlExtractor**

```csharp
// src/Iaet.JsAnalysis/GraphQlExtractor.cs
using System.Text.RegularExpressions;

namespace Iaet.JsAnalysis;

public static partial class GraphQlExtractor
{
    public static IReadOnlyList<string> Extract(string jsContent)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<string>();
        foreach (Match match in QueryPattern().Matches(jsContent))
        {
            results.Add(match.Groups[1].Value);
        }

        foreach (Match match in MutationPattern().Matches(jsContent))
        {
            results.Add(match.Groups[1].Value);
        }

        return results;
    }

    [GeneratedRegex("""["'`](query\s+\w+[^"'`]*)["'`]""")]
    private static partial Regex QueryPattern();

    [GeneratedRegex("""["'`](mutation\s+\w+[^"'`]*)["'`]""")]
    private static partial Regex MutationPattern();
}
```

- [ ] **Step 4: Implement ConfigExtractor**

```csharp
// src/Iaet.JsAnalysis/ConfigExtractor.cs
using System.Text.RegularExpressions;

namespace Iaet.JsAnalysis;

public static partial class ConfigExtractor
{
    public static IReadOnlyList<KeyValuePair<string, string>> Extract(string jsContent)
    {
        if (string.IsNullOrEmpty(jsContent))
            return [];

        var results = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ConstAssignmentPattern().Matches(jsContent))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (seen.Add(key))
                results.Add(new KeyValuePair<string, string>(key, value));
        }

        foreach (Match match in ObjectPropertyUrlPattern().Matches(jsContent))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (seen.Add(key))
                results.Add(new KeyValuePair<string, string>(key, value));
        }

        foreach (Match match in ConstBoolPattern().Matches(jsContent))
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (seen.Add(key))
                results.Add(new KeyValuePair<string, string>(key, value));
        }

        return results;
    }

    [GeneratedRegex("""const\s+([A-Z][A-Z0-9_]+)\s*=\s*["'`](https?://[^"'`]+)["'`]""")]
    private static partial Regex ConstAssignmentPattern();

    [GeneratedRegex("""(\w+(?:Url|Api|Endpoint|Base))\s*:\s*["'`](https?://[^"'`]+)["'`]""", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectPropertyUrlPattern();

    [GeneratedRegex("""const\s+([A-Z][A-Z0-9_]+)\s*=\s*(true|false)""")]
    private static partial Regex ConstBoolPattern();
}
```

- [ ] **Step 5: Create BundleAnalysisResult**

```csharp
// src/Iaet.JsAnalysis/BundleAnalysisResult.cs
using Iaet.Core.Models;

namespace Iaet.JsAnalysis;

public sealed record BundleAnalysisResult
{
    public required string SourceFile { get; init; }
    public IReadOnlyList<ExtractedUrl> Urls { get; init; } = [];
    public IReadOnlyList<ExtractedUrl> FetchCalls { get; init; } = [];
    public IReadOnlyList<ExtractedUrl> WebSocketUrls { get; init; } = [];
    public IReadOnlyList<string> GraphQlQueries { get; init; } = [];
    public IReadOnlyList<KeyValuePair<string, string>> ConfigEntries { get; init; } = [];
    public IReadOnlyList<string> GoDeeper { get; init; } = [];
}
```

- [ ] **Step 6: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.JsAnalysis/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.JsAnalysis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetJsAnalysis(this IServiceCollection services)
    {
        return services;
    }
}
```

- [ ] **Step 7: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.JsAnalysis.Tests/ -v quiet`

```bash
git add src/Iaet.JsAnalysis/ tests/Iaet.JsAnalysis.Tests/
git commit -m "feat(js): implement GraphQlExtractor, ConfigExtractor, and BundleAnalysisResult"
```

---

## Task 6: WebSocket Stream Analyzer

**Files:**
- Create: `src/Iaet.ProtocolAnalysis/IStreamAnalyzer.cs`
- Create: `src/Iaet.ProtocolAnalysis/StreamAnalysis.cs`
- Create: `src/Iaet.ProtocolAnalysis/WebSocketAnalyzer.cs`
- Test: `tests/Iaet.ProtocolAnalysis.Tests/WebSocketAnalyzerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.ProtocolAnalysis.Tests/WebSocketAnalyzerTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class WebSocketAnalyzerTests
{
    [Fact]
    public void Analyze_classifies_json_messages()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("""{"type":"connection_init"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"connection_ack"}""", StreamFrameDirection.Received),
            MakeFrame("""{"type":"data","payload":{"user":"test"}}""", StreamFrameDirection.Received),
        ]);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().Contain(["connection_init", "connection_ack", "data"]);
    }

    [Fact]
    public void Analyze_detects_graphql_ws_subprotocol()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("""{"type":"connection_init"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"connection_ack"}""", StreamFrameDirection.Received),
            MakeFrame("""{"type":"subscribe","payload":{"query":"{ users { id } }"}}""", StreamFrameDirection.Sent),
        ],
        subprotocol: "graphql-ws");

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.SubProtocol.Should().Be("graphql-ws");
        result.Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Analyze_detects_heartbeat_patterns()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("""{"type":"ping"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"pong"}""", StreamFrameDirection.Received),
            MakeFrame("""{"type":"ping"}""", StreamFrameDirection.Sent),
            MakeFrame("""{"type":"pong"}""", StreamFrameDirection.Received),
        ]);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().Contain(["ping", "pong"]);
        result.HasHeartbeat.Should().BeTrue();
    }

    [Fact]
    public void Analyze_handles_non_json_frames()
    {
        var stream = MakeStream(StreamProtocol.WebSocket,
        [
            MakeFrame("plain text message", StreamFrameDirection.Sent),
        ]);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().Contain("text");
    }

    [Fact]
    public void Analyze_handles_empty_frames()
    {
        var stream = MakeStream(StreamProtocol.WebSocket, []);

        var result = new WebSocketAnalyzer().Analyze(stream);

        result.MessageTypes.Should().BeEmpty();
    }

    [Fact]
    public void CanAnalyze_returns_true_for_websocket()
    {
        var analyzer = new WebSocketAnalyzer();
        analyzer.CanAnalyze(StreamProtocol.WebSocket).Should().BeTrue();
        analyzer.CanAnalyze(StreamProtocol.ServerSentEvents).Should().BeFalse();
    }

    private static CapturedStream MakeStream(StreamProtocol protocol, IReadOnlyList<StreamFrame> frames, string? subprotocol = null)
    {
        var metadata = new Dictionary<string, string>();
        if (subprotocol is not null)
            metadata["subprotocol"] = subprotocol;

        return new CapturedStream
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Protocol = protocol,
            Url = "wss://example.com/ws",
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = new StreamMetadata(metadata),
            Frames = frames,
        };
    }

    private static StreamFrame MakeFrame(string text, StreamFrameDirection direction) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Direction = direction,
        TextPayload = text,
        SizeBytes = text.Length,
    };
}
```

- [ ] **Step 2: Create IStreamAnalyzer contract and StreamAnalysis result**

```csharp
// src/Iaet.ProtocolAnalysis/IStreamAnalyzer.cs
using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

public interface IStreamAnalyzer
{
    bool CanAnalyze(StreamProtocol protocol);
    StreamAnalysis Analyze(CapturedStream stream);
}
```

```csharp
// src/Iaet.ProtocolAnalysis/StreamAnalysis.cs
using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

public sealed record StreamAnalysis
{
    public required Guid StreamId { get; init; }
    public required StreamProtocol Protocol { get; init; }
    public IReadOnlyList<string> MessageTypes { get; init; } = [];
    public string? SubProtocol { get; init; }
    public bool HasHeartbeat { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public StateMachineModel? StateMachine { get; init; }
}
```

- [ ] **Step 3: Implement WebSocketAnalyzer**

```csharp
// src/Iaet.ProtocolAnalysis/WebSocketAnalyzer.cs
using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

public sealed class WebSocketAnalyzer : IStreamAnalyzer
{
    private static readonly HashSet<string> HeartbeatTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ping", "pong", "heartbeat", "ka",
    };

    public bool CanAnalyze(StreamProtocol protocol) => protocol == StreamProtocol.WebSocket;

    public StreamAnalysis Analyze(CapturedStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var messageTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasHeartbeat = false;

        if (stream.Frames is not null)
        {
            foreach (var frame in stream.Frames)
            {
                var msgType = ClassifyFrame(frame);
                messageTypes.Add(msgType);

                if (HeartbeatTypes.Contains(msgType))
                    hasHeartbeat = true;
            }
        }

        var subProtocol = stream.Metadata.Properties.TryGetValue("subprotocol", out var sp) ? sp : null;
        var confidence = subProtocol is not null ? ConfidenceLevel.High : ConfidenceLevel.Medium;

        return new StreamAnalysis
        {
            StreamId = stream.Id,
            Protocol = StreamProtocol.WebSocket,
            MessageTypes = messageTypes.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            SubProtocol = subProtocol,
            HasHeartbeat = hasHeartbeat,
            Confidence = confidence,
        };
    }

    private static string ClassifyFrame(StreamFrame frame)
    {
        if (frame.TextPayload is null)
            return "binary";

        try
        {
            using var doc = JsonDocument.Parse(frame.TextPayload);
            if (doc.RootElement.TryGetProperty("type", out var typeProp))
                return typeProp.GetString() ?? "json";

            if (doc.RootElement.TryGetProperty("event", out var eventProp))
                return eventProp.GetString() ?? "json";

            return "json";
        }
        catch (JsonException)
        {
            return "text";
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.ProtocolAnalysis.Tests/ --filter "FullyQualifiedName~WebSocketAnalyzerTests" -v quiet`

```bash
git add src/Iaet.ProtocolAnalysis/ tests/Iaet.ProtocolAnalysis.Tests/WebSocketAnalyzerTests.cs
git commit -m "feat(protocol): implement WebSocketAnalyzer with message classification"
```

---

## Task 7: SDP Parser

**Files:**
- Create: `src/Iaet.ProtocolAnalysis/SdpParser.cs`
- Test: `tests/Iaet.ProtocolAnalysis.Tests/SdpParserTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.ProtocolAnalysis.Tests/SdpParserTests.cs
using FluentAssertions;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class SdpParserTests
{
    private const string SampleSdp = """
        v=0
        o=- 1234567890 2 IN IP4 127.0.0.1
        s=-
        t=0 0
        a=group:BUNDLE 0 1
        m=audio 9 UDP/TLS/RTP/SAVPF 111 103
        c=IN IP4 0.0.0.0
        a=rtpmap:111 opus/48000/2
        a=rtpmap:103 ISAC/16000
        a=ice-ufrag:abc123
        a=ice-pwd:def456
        a=fingerprint:sha-256 AA:BB:CC
        m=video 9 UDP/TLS/RTP/SAVPF 96 97
        a=rtpmap:96 VP8/90000
        a=rtpmap:97 H264/90000
        """;

    [Fact]
    public void Parse_extracts_media_sections()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.MediaSections.Should().HaveCount(2);
        result.MediaSections[0].Type.Should().Be("audio");
        result.MediaSections[1].Type.Should().Be("video");
    }

    [Fact]
    public void Parse_extracts_codecs()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.MediaSections[0].Codecs.Should().Contain(["opus/48000/2", "ISAC/16000"]);
        result.MediaSections[1].Codecs.Should().Contain(["VP8/90000", "H264/90000"]);
    }

    [Fact]
    public void Parse_extracts_ice_credentials()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.IceUfrag.Should().Be("abc123");
        result.IcePwd.Should().Be("def456");
    }

    [Fact]
    public void Parse_extracts_fingerprint()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.Fingerprint.Should().Be("sha-256 AA:BB:CC");
    }

    [Fact]
    public void Parse_extracts_bundle_group()
    {
        var result = SdpParser.Parse(SampleSdp);

        result.BundleGroup.Should().Be("0 1");
    }

    [Fact]
    public void Parse_handles_empty_sdp()
    {
        var result = SdpParser.Parse("");

        result.MediaSections.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Implement SdpParser**

```csharp
// src/Iaet.ProtocolAnalysis/SdpParser.cs
namespace Iaet.ProtocolAnalysis;

public static class SdpParser
{
    public static SdpResult Parse(string sdp)
    {
        if (string.IsNullOrWhiteSpace(sdp))
            return new SdpResult();

        var lines = sdp.Split('\n', StringSplitOptions.TrimEntries);
        var mediaSections = new List<SdpMediaSection>();
        SdpMediaSection? currentMedia = null;
        string? iceUfrag = null, icePwd = null, fingerprint = null, bundleGroup = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("m=", StringComparison.Ordinal))
            {
                currentMedia = new SdpMediaSection { Type = line[2..].Split(' ')[0] };
                mediaSections.Add(currentMedia);
            }
            else if (line.StartsWith("a=rtpmap:", StringComparison.Ordinal))
            {
                var parts = line["a=rtpmap:".Length..].Split(' ', 2);
                if (parts.Length == 2 && currentMedia is not null)
                    currentMedia.CodecList.Add(parts[1]);
            }
            else if (line.StartsWith("a=ice-ufrag:", StringComparison.Ordinal))
            {
                iceUfrag = line["a=ice-ufrag:".Length..];
            }
            else if (line.StartsWith("a=ice-pwd:", StringComparison.Ordinal))
            {
                icePwd = line["a=ice-pwd:".Length..];
            }
            else if (line.StartsWith("a=fingerprint:", StringComparison.Ordinal))
            {
                fingerprint = line["a=fingerprint:".Length..];
            }
            else if (line.StartsWith("a=group:BUNDLE", StringComparison.Ordinal))
            {
                bundleGroup = line["a=group:BUNDLE ".Length..];
            }
        }

        return new SdpResult
        {
            MediaSections = mediaSections,
            IceUfrag = iceUfrag,
            IcePwd = icePwd,
            Fingerprint = fingerprint,
            BundleGroup = bundleGroup,
        };
    }
}

public sealed class SdpMediaSection
{
    public required string Type { get; init; }
    internal List<string> CodecList { get; } = [];
    public IReadOnlyList<string> Codecs => CodecList;
}

public sealed record SdpResult
{
    public IReadOnlyList<SdpMediaSection> MediaSections { get; init; } = [];
    public string? IceUfrag { get; init; }
    public string? IcePwd { get; init; }
    public string? Fingerprint { get; init; }
    public string? BundleGroup { get; init; }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.ProtocolAnalysis.Tests/ --filter "FullyQualifiedName~SdpParserTests" -v quiet`

```bash
git add src/Iaet.ProtocolAnalysis/SdpParser.cs tests/Iaet.ProtocolAnalysis.Tests/SdpParserTests.cs
git commit -m "feat(protocol): implement SdpParser for WebRTC SDP offer/answer parsing"
```

---

## Task 8: StateMachineBuilder and MediaManifestAnalyzer

**Files:**
- Create: `src/Iaet.ProtocolAnalysis/StateMachineBuilder.cs`
- Create: `src/Iaet.ProtocolAnalysis/MediaManifestAnalyzer.cs`
- Create: `src/Iaet.ProtocolAnalysis/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.ProtocolAnalysis.Tests/StateMachineBuilderTests.cs`
- Test: `tests/Iaet.ProtocolAnalysis.Tests/MediaManifestAnalyzerTests.cs`

- [ ] **Step 1: Write StateMachineBuilder tests**

```csharp
// tests/Iaet.ProtocolAnalysis.Tests/StateMachineBuilderTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class StateMachineBuilderTests
{
    [Fact]
    public void Build_from_ordered_message_types()
    {
        var messageSequence = new[] { "connection_init", "connection_ack", "subscribe", "data", "complete" };

        var sm = StateMachineBuilder.Build("GraphQL-WS", messageSequence);

        sm.Name.Should().Be("GraphQL-WS");
        sm.InitialState.Should().Be("connection_init");
        sm.States.Should().Contain(["connection_init", "connection_ack", "subscribe", "data", "complete"]);
        sm.Transitions.Should().HaveCount(4);
        sm.Transitions[0].From.Should().Be("connection_init");
        sm.Transitions[0].To.Should().Be("connection_ack");
    }

    [Fact]
    public void Build_deduplicates_transitions()
    {
        var messages = new[] { "ping", "pong", "ping", "pong", "data" };

        var sm = StateMachineBuilder.Build("WS", messages);

        sm.Transitions.Should().HaveCount(3); // ping→pong, pong→ping, pong→data (deduped)
    }

    [Fact]
    public void Build_handles_empty_sequence()
    {
        var sm = StateMachineBuilder.Build("Empty", []);

        sm.States.Should().BeEmpty();
        sm.Transitions.Should().BeEmpty();
    }

    [Fact]
    public void Build_handles_single_message()
    {
        var sm = StateMachineBuilder.Build("Single", ["init"]);

        sm.States.Should().Contain("init");
        sm.Transitions.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Write MediaManifestAnalyzer tests**

```csharp
// tests/Iaet.ProtocolAnalysis.Tests/MediaManifestAnalyzerTests.cs
using FluentAssertions;
using Iaet.ProtocolAnalysis;

namespace Iaet.ProtocolAnalysis.Tests;

public sealed class MediaManifestAnalyzerTests
{
    [Fact]
    public void AnalyzeHls_extracts_variants()
    {
        var manifest = """
            #EXTM3U
            #EXT-X-STREAM-INF:BANDWIDTH=1000000,RESOLUTION=640x360,CODECS="avc1.42e00a,mp4a.40.2"
            low/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1280x720,CODECS="avc1.4d401f,mp4a.40.2"
            mid/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=6000000,RESOLUTION=1920x1080,CODECS="avc1.640028,mp4a.40.2"
            high/index.m3u8
            """;

        var result = MediaManifestAnalyzer.AnalyzeHls(manifest);

        result.Variants.Should().HaveCount(3);
        result.Variants[0].Resolution.Should().Be("640x360");
        result.Variants[2].Resolution.Should().Be("1920x1080");
    }

    [Fact]
    public void AnalyzeHls_handles_empty_manifest()
    {
        var result = MediaManifestAnalyzer.AnalyzeHls("");
        result.Variants.Should().BeEmpty();
    }

    [Fact]
    public void DetectFormat_identifies_hls_vs_dash()
    {
        MediaManifestAnalyzer.DetectFormat("#EXTM3U\n").Should().Be("HLS");
        MediaManifestAnalyzer.DetectFormat("<MPD xmlns=").Should().Be("DASH");
        MediaManifestAnalyzer.DetectFormat("unknown").Should().Be("Unknown");
    }
}
```

- [ ] **Step 3: Implement StateMachineBuilder**

```csharp
// src/Iaet.ProtocolAnalysis/StateMachineBuilder.cs
using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

public static class StateMachineBuilder
{
    public static StateMachineModel Build(string name, IReadOnlyList<string> messageSequence)
    {
        if (messageSequence.Count == 0)
            return new StateMachineModel { Name = name, States = [], Transitions = [], InitialState = string.Empty };

        var states = new LinkedHashSet(messageSequence);
        var transitions = new List<StateTransition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < messageSequence.Count - 1; i++)
        {
            var key = $"{messageSequence[i]}→{messageSequence[i + 1]}";
            if (seen.Add(key))
            {
                transitions.Add(new StateTransition
                {
                    From = messageSequence[i],
                    To = messageSequence[i + 1],
                    Trigger = messageSequence[i + 1],
                });
            }
        }

        return new StateMachineModel
        {
            Name = name,
            States = states.ToList(),
            Transitions = transitions,
            InitialState = messageSequence[0],
        };
    }

    private sealed class LinkedHashSet(IEnumerable<string> source)
    {
        private readonly HashSet<string> _set = new(StringComparer.Ordinal);
        private readonly List<string> _list = [];

        public List<string> ToList()
        {
            foreach (var item in source)
            {
                if (_set.Add(item))
                    _list.Add(item);
            }
            return _list;
        }
    }
}
```

- [ ] **Step 4: Implement MediaManifestAnalyzer**

```csharp
// src/Iaet.ProtocolAnalysis/MediaManifestAnalyzer.cs
using System.Text.RegularExpressions;

namespace Iaet.ProtocolAnalysis;

public static partial class MediaManifestAnalyzer
{
    public static HlsManifestResult AnalyzeHls(string manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest))
            return new HlsManifestResult();

        var variants = new List<HlsVariant>();
        var lines = manifest.Split('\n', StringSplitOptions.TrimEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var match = StreamInfPattern().Match(lines[i]);
            if (!match.Success)
                continue;

            var bandwidth = match.Groups[1].Value;
            var resMatch = ResolutionPattern().Match(lines[i]);
            var codecsMatch = CodecsPattern().Match(lines[i]);
            var uri = i + 1 < lines.Length ? lines[i + 1] : null;

            variants.Add(new HlsVariant
            {
                Bandwidth = int.TryParse(bandwidth, out var bw) ? bw : 0,
                Resolution = resMatch.Success ? resMatch.Groups[1].Value : null,
                Codecs = codecsMatch.Success ? codecsMatch.Groups[1].Value : null,
                Uri = uri,
            });
        }

        return new HlsManifestResult { Variants = variants };
    }

    public static string DetectFormat(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Unknown";

        if (content.Contains("#EXTM3U", StringComparison.Ordinal))
            return "HLS";

        if (content.Contains("<MPD", StringComparison.Ordinal))
            return "DASH";

        return "Unknown";
    }

    [GeneratedRegex("""#EXT-X-STREAM-INF:.*?BANDWIDTH=(\d+)""")]
    private static partial Regex StreamInfPattern();

    [GeneratedRegex("""RESOLUTION=(\d+x\d+)""")]
    private static partial Regex ResolutionPattern();

    [GeneratedRegex("""CODECS="([^"]+)"""")]
    private static partial Regex CodecsPattern();
}

public sealed record HlsManifestResult
{
    public IReadOnlyList<HlsVariant> Variants { get; init; } = [];
}

public sealed record HlsVariant
{
    public int Bandwidth { get; init; }
    public string? Resolution { get; init; }
    public string? Codecs { get; init; }
    public string? Uri { get; init; }
}
```

- [ ] **Step 5: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.ProtocolAnalysis/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.ProtocolAnalysis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetProtocolAnalysis(this IServiceCollection services)
    {
        services.AddSingleton<IStreamAnalyzer, WebSocketAnalyzer>();
        return services;
    }
}
```

- [ ] **Step 6: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.ProtocolAnalysis.Tests/ -v quiet`

```bash
git add src/Iaet.ProtocolAnalysis/ tests/Iaet.ProtocolAnalysis.Tests/
git commit -m "feat(protocol): implement StateMachineBuilder, MediaManifestAnalyzer, and DI registration"
```

---

## Task 9: Schema Extensions — DependencyGraphBuilder and AuthChainDetector

**Files:**
- Create: `src/Iaet.Schema/DependencyGraphBuilder.cs`
- Create: `src/Iaet.Schema/AuthChainDetector.cs`
- Test: `tests/Iaet.Schema.Tests/DependencyGraphBuilderTests.cs`
- Test: `tests/Iaet.Schema.Tests/AuthChainDetectorTests.cs`

- [ ] **Step 1: Write DependencyGraphBuilder tests**

```csharp
// tests/Iaet.Schema.Tests/DependencyGraphBuilderTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class DependencyGraphBuilderTests
{
    [Fact]
    public void Build_detects_shared_ids_in_responses_and_requests()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/session", responseBody: """{"sessionId":"abc123"}"""),
            MakeRequest("GET", "/api/calls", requestHeaders: new() { ["X-Session-Id"] = "abc123" }),
        };

        var deps = DependencyGraphBuilder.Build(requests);

        deps.Should().Contain(d => d.From.Contains("/session") && d.To.Contains("/calls"));
    }

    [Fact]
    public void Build_detects_token_in_response_used_in_later_request_url()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/api/auth", responseBody: """{"token":"tok_xyz789"}"""),
            MakeRequest("GET", "/api/data?token=tok_xyz789"),
        };

        var deps = DependencyGraphBuilder.Build(requests);

        deps.Should().Contain(d => d.From.Contains("/auth") && d.To.Contains("/data"));
    }

    [Fact]
    public void Build_returns_empty_for_independent_requests()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/users"),
            MakeRequest("GET", "/api/products"),
        };

        var deps = DependencyGraphBuilder.Build(requests);

        deps.Should().BeEmpty();
    }

    private static CapturedRequest MakeRequest(string method, string url, string? responseBody = null, Dictionary<string, string>? requestHeaders = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = $"https://example.com{url}",
        RequestHeaders = requestHeaders ?? new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        ResponseBody = responseBody,
        DurationMs = 50,
    };
}
```

- [ ] **Step 2: Write AuthChainDetector tests**

```csharp
// tests/Iaet.Schema.Tests/AuthChainDetectorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class AuthChainDetectorTests
{
    [Fact]
    public void Detect_finds_auth_header_chains()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("POST", "/login", responseBody: """{"access_token":"eyJ.payload.sig"}"""),
            MakeRequest("GET", "/api/data", requestHeaders: new() { ["Authorization"] = "<REDACTED>" }),
        };

        var chains = AuthChainDetector.Detect(requests);

        chains.Should().NotBeEmpty();
        chains[0].Steps.Should().Contain(s => s.Endpoint.Contains("/login"));
    }

    [Fact]
    public void Detect_returns_empty_when_no_auth_patterns()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/public/data"),
        };

        var chains = AuthChainDetector.Detect(requests);

        chains.Should().BeEmpty();
    }

    private static CapturedRequest MakeRequest(string method, string url, string? responseBody = null, Dictionary<string, string>? requestHeaders = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = $"https://example.com{url}",
        RequestHeaders = requestHeaders ?? new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        ResponseBody = responseBody,
        DurationMs = 50,
    };
}
```

- [ ] **Step 3: Implement DependencyGraphBuilder**

```csharp
// src/Iaet.Schema/DependencyGraphBuilder.cs
using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.Schema;

public static class DependencyGraphBuilder
{
    private const int MinTokenLength = 6;

    public static IReadOnlyList<RequestDependency> Build(IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var responseTokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var dependencies = new List<RequestDependency>();

        foreach (var req in requests)
        {
            var signature = $"{req.HttpMethod} {new Uri(req.Url).AbsolutePath}";
            if (req.ResponseBody is not null)
            {
                foreach (var (key, value) in ExtractJsonValues(req.ResponseBody))
                {
                    if (value.Length >= MinTokenLength)
                        responseTokens[value] = signature;
                }
            }
        }

        foreach (var req in requests)
        {
            var signature = $"{req.HttpMethod} {new Uri(req.Url).AbsolutePath}";

            foreach (var (headerKey, headerValue) in req.RequestHeaders)
            {
                if (headerValue == "<REDACTED>")
                    continue;

                if (responseTokens.TryGetValue(headerValue, out var source) && source != signature)
                {
                    dependencies.Add(new RequestDependency
                    {
                        From = source,
                        To = signature,
                        Reason = $"{headerKey} header contains value from response",
                        SharedField = headerKey,
                    });
                }
            }

            if (req.Url.Contains('?', StringComparison.Ordinal))
            {
                var query = new Uri(req.Url).Query;
                foreach (var (tokenValue, source) in responseTokens)
                {
                    if (source != signature && query.Contains(tokenValue, StringComparison.Ordinal))
                    {
                        dependencies.Add(new RequestDependency
                        {
                            From = source,
                            To = signature,
                            Reason = "Query parameter contains value from response",
                        });
                    }
                }
            }
        }

        return dependencies;
    }

    private static IEnumerable<KeyValuePair<string, string>> ExtractJsonValues(string body)
    {
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (val is not null)
                        yield return new KeyValuePair<string, string>(prop.Name, val);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Implement AuthChainDetector**

```csharp
// src/Iaet.Schema/AuthChainDetector.cs
using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.Schema;

public static class AuthChainDetector
{
    private static readonly HashSet<string> AuthResponseFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token", "token", "auth_token", "jwt", "session_token", "id_token", "refresh_token",
    };

    private static readonly HashSet<string> AuthHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "x-auth-token", "x-session-id", "x-csrf-token",
    };

    public static IReadOnlyList<AuthChain> Detect(IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var providers = new List<AuthChainStep>();
        var consumers = new List<AuthChainStep>();

        foreach (var req in requests)
        {
            var sig = $"{req.HttpMethod} {new Uri(req.Url).AbsolutePath}";

            if (req.ResponseBody is not null)
            {
                foreach (var field in ExtractAuthFields(req.ResponseBody))
                {
                    providers.Add(new AuthChainStep { Endpoint = sig, Provides = field, Type = "token" });
                }
            }

            foreach (var header in req.RequestHeaders.Keys)
            {
                if (AuthHeaders.Contains(header))
                {
                    consumers.Add(new AuthChainStep { Endpoint = sig, Consumes = header, Type = "header" });
                }
            }
        }

        if (providers.Count == 0 && consumers.Count == 0)
            return [];

        var steps = new List<AuthChainStep>();
        steps.AddRange(providers);
        steps.AddRange(consumers);

        return [new AuthChain { Name = "Detected auth chain", Steps = steps }];
    }

    private static IEnumerable<string> ExtractAuthFields(string body)
    {
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (AuthResponseFields.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.String)
                    yield return prop.Name;
            }
        }
    }
}
```

- [ ] **Step 5: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Schema.Tests/ --filter "FullyQualifiedName~DependencyGraphBuilderTests|FullyQualifiedName~AuthChainDetectorTests" -v quiet`

```bash
git add src/Iaet.Schema/ tests/Iaet.Schema.Tests/
git commit -m "feat(schema): implement DependencyGraphBuilder and AuthChainDetector"
```

---

## Task 10: Schema Extensions — RateLimitDetector and Non-JSON Robustness

**Files:**
- Create: `src/Iaet.Schema/RateLimitDetector.cs`
- Modify: `src/Iaet.Schema/JsonTypeMap.cs` — improve non-JSON handling
- Test: `tests/Iaet.Schema.Tests/RateLimitDetectorTests.cs`
- Test: `tests/Iaet.Schema.Tests/JsonTypeMapRobustnessTests.cs`

- [ ] **Step 1: Write RateLimitDetector tests**

```csharp
// tests/Iaet.Schema.Tests/RateLimitDetectorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class RateLimitDetectorTests
{
    [Fact]
    public void Detect_finds_429_responses()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "/api/users", 200),
            MakeRequest("GET", "/api/users", 429, responseHeaders: new() { ["Retry-After"] = "30" }),
            MakeRequest("GET", "/api/users", 200),
        };

        var result = RateLimitDetector.Detect(requests);

        result.Should().Contain(r => r.Endpoint.Contains("/users") && r.RetryAfterSeconds == 30);
    }

    [Fact]
    public void Detect_returns_empty_for_no_429s()
    {
        var requests = new List<CapturedRequest> { MakeRequest("GET", "/api/data", 200) };

        RateLimitDetector.Detect(requests).Should().BeEmpty();
    }

    [Fact]
    public void Detect_handles_missing_retry_after()
    {
        var requests = new List<CapturedRequest> { MakeRequest("GET", "/api/data", 429) };

        var result = RateLimitDetector.Detect(requests);

        result.Should().HaveCount(1);
        result[0].RetryAfterSeconds.Should().BeNull();
    }

    private static CapturedRequest MakeRequest(string method, string url, int status, Dictionary<string, string>? responseHeaders = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = $"https://example.com{url}",
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = status,
        ResponseHeaders = responseHeaders ?? new Dictionary<string, string>(),
        DurationMs = 50,
    };
}
```

- [ ] **Step 2: Write JsonTypeMap robustness tests**

```csharp
// tests/Iaet.Schema.Tests/JsonTypeMapRobustnessTests.cs
using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public sealed class JsonTypeMapRobustnessTests
{
    [Fact]
    public void TryAnalyze_handles_jsonp_prefix()
    {
        var result = JsonTypeMap.TryAnalyze(""");}\n{"key":"val"}""");
        result.Should().BeNull();
    }

    [Fact]
    public void TryAnalyze_handles_html_response()
    {
        var result = JsonTypeMap.TryAnalyze("<!DOCTYPE html><html><body>Error</body></html>");
        result.Should().BeNull();
    }

    [Fact]
    public void TryAnalyze_handles_bom_prefix()
    {
        var result = JsonTypeMap.TryAnalyze("\uFEFF{\"key\":\"val\"}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void TryAnalyze_handles_xss_protection_prefix()
    {
        var result = JsonTypeMap.TryAnalyze(")]}'\\n{\"key\":\"val\"}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void TryAnalyze_handles_empty_string()
    {
        JsonTypeMap.TryAnalyze("").Should().BeNull();
    }

    [Fact]
    public void TryAnalyze_handles_null()
    {
        JsonTypeMap.TryAnalyze(null!).Should().BeNull();
    }
}
```

- [ ] **Step 3: Implement RateLimitDetector**

```csharp
// src/Iaet.Schema/RateLimitDetector.cs
using System.Globalization;
using Iaet.Core.Models;

namespace Iaet.Schema;

public static class RateLimitDetector
{
    public static IReadOnlyList<RateLimitInfo> Detect(IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var results = new Dictionary<string, RateLimitInfo>(StringComparer.Ordinal);

        foreach (var req in requests)
        {
            if (req.ResponseStatus != 429)
                continue;

            var sig = $"{req.HttpMethod} {new Uri(req.Url).AbsolutePath}";
            int? retryAfter = null;

            if (req.ResponseHeaders.TryGetValue("Retry-After", out var retryVal) &&
                int.TryParse(retryVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                retryAfter = seconds;
            }

            results.TryAdd(sig, new RateLimitInfo
            {
                Endpoint = sig,
                RetryAfterSeconds = retryAfter,
            });
        }

        return results.Values.ToList();
    }
}

public sealed record RateLimitInfo
{
    public required string Endpoint { get; init; }
    public int? RetryAfterSeconds { get; init; }
}
```

- [ ] **Step 4: Fix JsonTypeMap non-JSON robustness**

Read `src/Iaet.Schema/JsonTypeMap.cs` first, then modify the `TryAnalyze` method to handle BOM and XSS protection prefixes. The current method should already reject HTML and JSONP — add BOM stripping and XSS prefix stripping:

At the start of `TryAnalyze`, before `JsonDocument.Parse`:

```csharp
if (string.IsNullOrWhiteSpace(json))
    return null;

// Strip BOM
if (json[0] == '\uFEFF')
    json = json[1..];

// Strip common XSS protection prefixes
if (json.StartsWith(")]}'", StringComparison.Ordinal))
{
    var newlineIdx = json.IndexOf('\n', StringComparison.Ordinal);
    if (newlineIdx >= 0)
        json = json[(newlineIdx + 1)..];
}
```

- [ ] **Step 5: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Schema.Tests/ -v quiet`

```bash
git add src/Iaet.Schema/ tests/Iaet.Schema.Tests/
git commit -m "feat(schema): implement RateLimitDetector and fix non-JSON body robustness"
```

---

## Task 11: Full Integration Verification

- [ ] **Step 1: Run full solution build**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded with 0 errors.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test Iaet.slnx -v quiet`
Expected: All tests pass.

- [ ] **Step 3: Verify new test counts**

JS Analysis tests: ~13 (UrlExtractor 7, FetchCall 4, WebSocket 2, GraphQL 2, Config 3)
Protocol Analysis tests: ~15 (WebSocket 6, SDP 6, StateMachine 4, Media 3)
Schema extension tests: ~9 (Dependency 3, AuthChain 2, RateLimit 3, Robustness 6)
Core model tests: 5

Total new: ~42 tests.

- [ ] **Step 4: Final commit if fixups needed**

```bash
git add -A
git commit -m "fix: integration fixups from Phase 3 smoke testing"
```

---

## Deferred Items

1. **BundleDownloader** — HTTP download of JS bundles from captured `<script>` tags. Requires network access and is better suited for integration testing. The extractors work on string input, so bundles can be downloaded separately and fed in.

2. **GrpcServiceExtractor** — Extracting service/method names from gRPC-Web binary frames requires protobuf wire format parsing. Deferred to a targeted follow-up since it's a specialized binary protocol.

3. **SharedIdTracer** — Tracing IDs across endpoints is partially covered by DependencyGraphBuilder (which checks response values appearing in later request headers/URLs). A dedicated SharedIdTracer with deeper JSON nesting traversal can be added incrementally.

4. **Node.js AST parsing** — The spec mentions Acorn-based AST parsing. The v1 implementation uses regex-based extraction which handles 80%+ of real-world patterns. AST parsing can be added as a v2 enhancement when regex proves insufficient for obfuscated bundles.

5. **SipAnalyzer** — SIP message parsing (INVITE, PRACK, BYE) requires dedicated SIP protocol knowledge. Deferred to a targeted follow-up.

6. **WebRtcSessionReconstructor** — Full WebRTC session lifecycle reconstruction from signaling captures. Deferred pending improved WebRTC CDP event coverage.

7. **BinaryFrameHeuristics** — Binary frame decoding (length-prefixed, protobuf, msgpack detection). Deferred to post-v1.

8. **ErrorClassifier** — Error response categorization by HTTP status and body patterns. Deferred to post-v1.
