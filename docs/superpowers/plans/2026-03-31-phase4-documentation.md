# Phase 4: Documentation & Diagrams Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add automated Mermaid diagram generation and investigation-focused export formats (narrative, coverage, human action items, secrets audit) to complete the agent team's documentation pipeline.

**Architecture:** One new assembly (`Iaet.Diagrams`) for Mermaid diagram generation from captured data. Extensions to `Iaet.Export` with 4 new generators following the existing static `Generate(ExportContext)` pattern. New CLI subcommands under `iaet export`. All generators consume the existing `ExportContext` — no changes to the data loading pipeline.

**Tech Stack:** .NET 10, Mermaid markdown syntax, System.Text.Json, xUnit + FluentAssertions + NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-31-agent-investigation-team-design.md` (Phase 4 section)

---

## File Structure

### New assembly: Iaet.Diagrams

```
src/Iaet.Diagrams/
  Iaet.Diagrams.csproj
  SequenceDiagramGenerator.cs       — request chains → Mermaid sequence diagram
  DataFlowMapGenerator.cs           — service topology → Mermaid flowchart
  StateMachineDiagramGenerator.cs   — protocol states → Mermaid stateDiagram
  DependencyGraphDiagramGenerator.cs — auth chains → Mermaid flowchart
  ConfidenceAnnotator.cs            — adds confidence notes to diagrams
  ServiceCollectionExtensions.cs
tests/Iaet.Diagrams.Tests/
  Iaet.Diagrams.Tests.csproj
  SequenceDiagramGeneratorTests.cs
  DataFlowMapGeneratorTests.cs
  StateMachineDiagramGeneratorTests.cs
  DependencyGraphDiagramGeneratorTests.cs
  ConfidenceAnnotatorTests.cs
```

### New generators in Iaet.Export

```
src/Iaet.Export/Generators/
  InvestigationNarrativeGenerator.cs    — round-by-round story
  CoverageReportGenerator.cs            — known vs observed endpoints
  HumanActionItemsGenerator.cs          — items needing manual investigation
  SecretsAuditGenerator.cs              — which secrets were used (not values)
tests/Iaet.Export.Tests/Generators/
  InvestigationNarrativeGeneratorTests.cs
  CoverageReportGeneratorTests.cs
  HumanActionItemsGeneratorTests.cs
  SecretsAuditGeneratorTests.cs
```

---

## Task 1: Solution Scaffolding — Iaet.Diagrams

**Files:**
- Create: `src/Iaet.Diagrams/Iaet.Diagrams.csproj`
- Create: `tests/Iaet.Diagrams.Tests/Iaet.Diagrams.Tests.csproj`
- Modify: `Iaet.slnx`

- [ ] **Step 1: Create project files**

```xml
<!-- src/Iaet.Diagrams/Iaet.Diagrams.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/Iaet.Diagrams.Tests/Iaet.Diagrams.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\Iaet.Diagrams\Iaet.Diagrams.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Update Iaet.slnx and verify build**

Add 2 entries. Run: `dotnet build Iaet.slnx`

- [ ] **Step 3: Commit**

```bash
git add src/Iaet.Diagrams/ tests/Iaet.Diagrams.Tests/ Iaet.slnx
git commit -m "chore: scaffold Iaet.Diagrams assembly"
```

---

## Task 2: SequenceDiagramGenerator

**Files:**
- Create: `src/Iaet.Diagrams/SequenceDiagramGenerator.cs`
- Test: `tests/Iaet.Diagrams.Tests/SequenceDiagramGeneratorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Diagrams.Tests/SequenceDiagramGeneratorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class SequenceDiagramGeneratorTests
{
    [Fact]
    public void Generate_produces_mermaid_sequence_diagram()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/session", 200, t: 1),
            MakeRequest("GET", "https://api.example.com/users/123", 200, t: 2),
            MakeRequest("POST", "https://api.example.com/messages", 201, t: 3),
        };

        var mermaid = SequenceDiagramGenerator.Generate("API Flow", requests);

        mermaid.Should().StartWith("sequenceDiagram");
        mermaid.Should().Contain("Browser");
        mermaid.Should().Contain("api.example.com");
        mermaid.Should().Contain("GET /session");
        mermaid.Should().Contain("200");
    }

    [Fact]
    public void Generate_groups_by_host()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/data", 200, t: 1),
            MakeRequest("GET", "https://auth.example.com/token", 200, t: 2),
        };

        var mermaid = SequenceDiagramGenerator.Generate("Multi-host", requests);

        mermaid.Should().Contain("api.example.com");
        mermaid.Should().Contain("auth.example.com");
    }

    [Fact]
    public void Generate_handles_error_responses()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/secret", 403, t: 1),
        };

        var mermaid = SequenceDiagramGenerator.Generate("Errors", requests);

        mermaid.Should().Contain("403");
    }

    [Fact]
    public void Generate_handles_empty_requests()
    {
        var mermaid = SequenceDiagramGenerator.Generate("Empty", []);

        mermaid.Should().Contain("sequenceDiagram");
        mermaid.Should().Contain("Note");
    }

    private static CapturedRequest MakeRequest(string method, string url, int status, int t) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow.AddSeconds(t),
        HttpMethod = method,
        Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = status,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 50,
    };
}
```

- [ ] **Step 2: Implement SequenceDiagramGenerator**

```csharp
// src/Iaet.Diagrams/SequenceDiagramGenerator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class SequenceDiagramGenerator
{
    public static string Generate(string title, IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var sb = new StringBuilder();
        sb.AppendLine("sequenceDiagram");
        sb.AppendLine($"    title {title}");

        if (requests.Count == 0)
        {
            sb.AppendLine("    Note over Browser: No requests captured");
            return sb.ToString();
        }

        sb.AppendLine("    participant Browser");

        var hosts = requests
            .Select(r => GetHost(r.Url))
            .Where(h => h is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var host in hosts)
        {
            sb.AppendLine($"    participant {SanitizeParticipant(host!)}");
        }

        var ordered = requests.OrderBy(r => r.Timestamp).ToList();
        foreach (var req in ordered)
        {
            var host = GetHost(req.Url);
            if (host is null) continue;

            var participant = SanitizeParticipant(host);
            var path = GetPath(req.Url);
            var arrow = req.ResponseStatus >= 400 ? "-->>" : "->>";
            var returnArrow = req.ResponseStatus >= 400 ? "--x" : "-->>";

            sb.AppendLine($"    Browser{arrow}{participant}: {req.HttpMethod} {path}");
            sb.AppendLine($"    {participant}{returnArrow}Browser: {req.ResponseStatus} ({req.DurationMs}ms)");
        }

        return sb.ToString();
    }

    private static string? GetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        return uri.Host;
    }

    private static string GetPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;
        var path = uri.AbsolutePath;
        return path.Length > 40 ? path[..40] + "..." : path;
    }

    private static string SanitizeParticipant(string name) =>
        name.Replace('.', '_').Replace('-', '_');
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Diagrams.Tests/ --filter "FullyQualifiedName~SequenceDiagramGeneratorTests" -v quiet`

```bash
git add src/Iaet.Diagrams/ tests/Iaet.Diagrams.Tests/
git commit -m "feat(diagrams): implement SequenceDiagramGenerator for Mermaid sequence diagrams"
```

---

## Task 3: DataFlowMapGenerator and DependencyGraphDiagramGenerator

**Files:**
- Create: `src/Iaet.Diagrams/DataFlowMapGenerator.cs`
- Create: `src/Iaet.Diagrams/DependencyGraphDiagramGenerator.cs`
- Test: `tests/Iaet.Diagrams.Tests/DataFlowMapGeneratorTests.cs`
- Test: `tests/Iaet.Diagrams.Tests/DependencyGraphDiagramGeneratorTests.cs`

- [ ] **Step 1: Write DataFlowMap tests**

```csharp
// tests/Iaet.Diagrams.Tests/DataFlowMapGeneratorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class DataFlowMapGeneratorTests
{
    [Fact]
    public void Generate_creates_flowchart_from_requests()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/data"),
            MakeRequest("POST", "https://auth.example.com/token"),
            MakeRequest("GET", "https://api.example.com/users"),
        };

        var mermaid = DataFlowMapGenerator.Generate("Service Map", requests);

        mermaid.Should().StartWith("flowchart TD");
        mermaid.Should().Contain("Browser");
        mermaid.Should().Contain("api_example_com");
        mermaid.Should().Contain("auth_example_com");
    }

    [Fact]
    public void Generate_shows_request_counts_on_edges()
    {
        var requests = new List<CapturedRequest>
        {
            MakeRequest("GET", "https://api.example.com/a"),
            MakeRequest("GET", "https://api.example.com/b"),
            MakeRequest("POST", "https://api.example.com/c"),
        };

        var mermaid = DataFlowMapGenerator.Generate("Map", requests);

        mermaid.Should().Contain("3 requests");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var mermaid = DataFlowMapGenerator.Generate("Empty", []);
        mermaid.Should().Contain("flowchart TD");
    }

    private static CapturedRequest MakeRequest(string method, string url) => new()
    {
        Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method, Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200, ResponseHeaders = new Dictionary<string, string>(), DurationMs = 50,
    };
}
```

- [ ] **Step 2: Write DependencyGraph tests**

```csharp
// tests/Iaet.Diagrams.Tests/DependencyGraphDiagramGeneratorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class DependencyGraphDiagramGeneratorTests
{
    [Fact]
    public void Generate_creates_flowchart_from_dependencies()
    {
        var deps = new List<RequestDependency>
        {
            new() { From = "POST /login", To = "GET /api/data", Reason = "Auth token required" },
            new() { From = "GET /session", To = "GET /api/calls", Reason = "Session ID required" },
        };

        var mermaid = DependencyGraphDiagramGenerator.Generate("Auth Dependencies", deps);

        mermaid.Should().StartWith("flowchart TD");
        mermaid.Should().Contain("POST /login");
        mermaid.Should().Contain("GET /api/data");
        mermaid.Should().Contain("Auth token required");
    }

    [Fact]
    public void Generate_from_auth_chains()
    {
        var chains = new List<AuthChain>
        {
            new()
            {
                Name = "Session flow",
                Steps =
                [
                    new AuthChainStep { Endpoint = "POST /login", Provides = "session_cookie", Type = "cookie" },
                    new AuthChainStep { Endpoint = "GET /api/data", Consumes = "session_cookie", Type = "cookie" },
                ],
            },
        };

        var mermaid = DependencyGraphDiagramGenerator.GenerateFromAuthChains("Auth Chains", chains);

        mermaid.Should().Contain("POST /login");
        mermaid.Should().Contain("session_cookie");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var mermaid = DependencyGraphDiagramGenerator.Generate("Empty", []);
        mermaid.Should().Contain("flowchart TD");
    }
}
```

- [ ] **Step 3: Implement DataFlowMapGenerator**

```csharp
// src/Iaet.Diagrams/DataFlowMapGenerator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class DataFlowMapGenerator
{
    public static string Generate(string title, IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine($"    %% {title}");

        if (requests.Count == 0)
            return sb.ToString();

        sb.AppendLine("    Browser([Browser])");

        var hostCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in requests)
        {
            var host = GetHost(req.Url);
            if (host is null) continue;

            if (!hostCounts.TryGetValue(host, out var count))
                count = 0;
            hostCounts[host] = count + 1;
        }

        foreach (var (host, count) in hostCounts)
        {
            var id = SanitizeId(host);
            sb.AppendLine($"    {id}[{host}]");
            sb.AppendLine($"    Browser -->|{count} requests| {id}");
        }

        return sb.ToString();
    }

    private static string? GetHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

    private static string SanitizeId(string name) =>
        name.Replace('.', '_').Replace('-', '_');
}
```

- [ ] **Step 4: Implement DependencyGraphDiagramGenerator**

```csharp
// src/Iaet.Diagrams/DependencyGraphDiagramGenerator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class DependencyGraphDiagramGenerator
{
    public static string Generate(string title, IReadOnlyList<RequestDependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine($"    %% {title}");

        if (dependencies.Count == 0)
            return sb.ToString();

        var nodeIndex = 0;
        var nodeMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var dep in dependencies)
        {
            var fromId = GetOrCreateNode(nodeMap, dep.From, ref nodeIndex);
            var toId = GetOrCreateNode(nodeMap, dep.To, ref nodeIndex);
            sb.AppendLine($"    {fromId} -->|\"{dep.Reason}\"| {toId}");
        }

        foreach (var (label, id) in nodeMap)
        {
            sb.AppendLine($"    {id}[\"{label}\"]");
        }

        return sb.ToString();
    }

    public static string GenerateFromAuthChains(string title, IReadOnlyList<AuthChain> chains)
    {
        ArgumentNullException.ThrowIfNull(chains);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine($"    %% {title}");

        var nodeIndex = 0;
        var nodeMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var chain in chains)
        {
            for (var i = 0; i < chain.Steps.Count - 1; i++)
            {
                var fromStep = chain.Steps[i];
                var toStep = chain.Steps[i + 1];

                var fromId = GetOrCreateNode(nodeMap, fromStep.Endpoint, ref nodeIndex);
                var toId = GetOrCreateNode(nodeMap, toStep.Endpoint, ref nodeIndex);
                var label = fromStep.Provides ?? toStep.Consumes ?? "depends";

                sb.AppendLine($"    {fromId} -->|\"{label}\"| {toId}");
            }
        }

        foreach (var (label, id) in nodeMap)
        {
            sb.AppendLine($"    {id}[\"{label}\"]");
        }

        return sb.ToString();
    }

    private static string GetOrCreateNode(Dictionary<string, string> map, string label, ref int index)
    {
        if (!map.TryGetValue(label, out var id))
        {
            id = $"N{index++}";
            map[label] = id;
        }
        return id;
    }
}
```

- [ ] **Step 5: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Diagrams.Tests/ -v quiet`

```bash
git add src/Iaet.Diagrams/ tests/Iaet.Diagrams.Tests/
git commit -m "feat(diagrams): implement DataFlowMapGenerator and DependencyGraphDiagramGenerator"
```

---

## Task 4: StateMachineDiagramGenerator and ConfidenceAnnotator

**Files:**
- Create: `src/Iaet.Diagrams/StateMachineDiagramGenerator.cs`
- Create: `src/Iaet.Diagrams/ConfidenceAnnotator.cs`
- Create: `src/Iaet.Diagrams/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Diagrams.Tests/StateMachineDiagramGeneratorTests.cs`
- Test: `tests/Iaet.Diagrams.Tests/ConfidenceAnnotatorTests.cs`

- [ ] **Step 1: Write StateMachineDiagram tests**

```csharp
// tests/Iaet.Diagrams.Tests/StateMachineDiagramGeneratorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class StateMachineDiagramGeneratorTests
{
    [Fact]
    public void Generate_creates_state_diagram()
    {
        var sm = new StateMachineModel
        {
            Name = "WebRTC",
            States = ["new", "connecting", "connected", "disconnected"],
            Transitions =
            [
                new StateTransition { From = "new", To = "connecting", Trigger = "createOffer" },
                new StateTransition { From = "connecting", To = "connected", Trigger = "iceComplete" },
                new StateTransition { From = "connected", To = "disconnected", Trigger = "close" },
            ],
            InitialState = "new",
        };

        var mermaid = StateMachineDiagramGenerator.Generate(sm);

        mermaid.Should().StartWith("stateDiagram-v2");
        mermaid.Should().Contain("[*] --> new");
        mermaid.Should().Contain("new --> connecting : createOffer");
        mermaid.Should().Contain("connecting --> connected : iceComplete");
    }

    [Fact]
    public void Generate_handles_empty_state_machine()
    {
        var sm = new StateMachineModel
        {
            Name = "Empty", States = [], Transitions = [], InitialState = "",
        };

        var mermaid = StateMachineDiagramGenerator.Generate(sm);

        mermaid.Should().Contain("stateDiagram-v2");
    }
}
```

- [ ] **Step 2: Write ConfidenceAnnotator tests**

```csharp
// tests/Iaet.Diagrams.Tests/ConfidenceAnnotatorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class ConfidenceAnnotatorTests
{
    [Fact]
    public void Annotate_adds_note_to_mermaid()
    {
        var diagram = "sequenceDiagram\n    Browser->>Server: GET /api";

        var annotated = ConfidenceAnnotator.Annotate(
            diagram, ConfidenceLevel.High, 5, "network-capture");

        annotated.Should().Contain("Confidence: High");
        annotated.Should().Contain("5 observations");
    }

    [Fact]
    public void Annotate_includes_limitations()
    {
        var diagram = "flowchart TD";

        var annotated = ConfidenceAnnotator.Annotate(
            diagram, ConfidenceLevel.Low, 0, "js-bundle-analyzer",
            ["Extracted from string literal only"]);

        annotated.Should().Contain("Confidence: Low");
        annotated.Should().Contain("Extracted from string literal only");
    }

    [Fact]
    public void Annotate_preserves_original_diagram()
    {
        var diagram = "stateDiagram-v2\n    [*] --> init";

        var annotated = ConfidenceAnnotator.Annotate(
            diagram, ConfidenceLevel.Medium, 2, "protocol-analyzer");

        annotated.Should().Contain("[*] --> init");
    }
}
```

- [ ] **Step 3: Implement StateMachineDiagramGenerator**

```csharp
// src/Iaet.Diagrams/StateMachineDiagramGenerator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class StateMachineDiagramGenerator
{
    public static string Generate(StateMachineModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");
        sb.AppendLine($"    %% {model.Name}");

        if (!string.IsNullOrEmpty(model.InitialState))
        {
            sb.AppendLine($"    [*] --> {model.InitialState}");
        }

        foreach (var transition in model.Transitions)
        {
            sb.AppendLine($"    {transition.From} --> {transition.To} : {transition.Trigger}");
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Implement ConfidenceAnnotator**

```csharp
// src/Iaet.Diagrams/ConfidenceAnnotator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class ConfidenceAnnotator
{
    public static string Annotate(
        string diagram,
        ConfidenceLevel confidence,
        int observationCount,
        string source,
        IReadOnlyList<string>? limitations = null)
    {
        var sb = new StringBuilder(diagram);

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"    %% Confidence: {confidence} — {observationCount} observations from {source}");

        if (limitations is not null && limitations.Count > 0)
        {
            foreach (var limitation in limitations)
            {
                sb.AppendLine($"    %% Limitation: {limitation}");
            }
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 5: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.Diagrams/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Diagrams;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetDiagrams(this IServiceCollection services)
    {
        return services;
    }
}
```

- [ ] **Step 6: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Diagrams.Tests/ -v quiet`

```bash
git add src/Iaet.Diagrams/ tests/Iaet.Diagrams.Tests/
git commit -m "feat(diagrams): implement StateMachineDiagramGenerator, ConfidenceAnnotator, and DI"
```

---

## Task 5: InvestigationNarrativeGenerator

**Files:**
- Create: `src/Iaet.Export/Generators/InvestigationNarrativeGenerator.cs`
- Test: `tests/Iaet.Export.Tests/Generators/InvestigationNarrativeGeneratorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Export.Tests/Generators/InvestigationNarrativeGeneratorTests.cs
using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class InvestigationNarrativeGeneratorTests
{
    [Fact]
    public void Generate_includes_session_header()
    {
        var ctx = TestContextFactory.MakeContext();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("# Investigation Report");
        narrative.Should().Contain("TestApp");
    }

    [Fact]
    public void Generate_includes_endpoint_summary()
    {
        var ctx = TestContextFactory.MakeContext();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("Endpoints Discovered");
        narrative.Should().Contain("GET /api/users/{id}");
    }

    [Fact]
    public void Generate_includes_stream_summary()
    {
        var ctx = TestContextFactory.MakeContextWithStreams();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("Streams");
        narrative.Should().Contain("WebSocket");
    }

    [Fact]
    public void Generate_includes_schema_summary()
    {
        var ctx = TestContextFactory.MakeContext();

        var narrative = InvestigationNarrativeGenerator.Generate(ctx);

        narrative.Should().Contain("Schema");
    }
}
```

- [ ] **Step 2: Implement InvestigationNarrativeGenerator**

```csharp
// src/Iaet.Export/Generators/InvestigationNarrativeGenerator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

public static class InvestigationNarrativeGenerator
{
    public static string Generate(ExportContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sb = new StringBuilder();

        sb.AppendLine("# Investigation Report");
        sb.AppendLine();
        sb.AppendLine($"**Target:** {ctx.Session.TargetApplication}");
        sb.AppendLine($"**Session:** {ctx.Session.Name}");
        sb.AppendLine($"**Started:** {ctx.Session.StartedAt:u}");
        sb.AppendLine($"**Requests:** {ctx.Requests.Count}");
        sb.AppendLine($"**Endpoints:** {ctx.EndpointGroups.Count}");
        sb.AppendLine($"**Streams:** {ctx.Streams.Count}");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();

        sb.AppendLine("## Endpoints Discovered");
        sb.AppendLine();

        if (ctx.EndpointGroups.Count == 0)
        {
            sb.AppendLine("No endpoints discovered.");
        }
        else
        {
            sb.AppendLine("| Endpoint | Observations | First Seen |");
            sb.AppendLine("|----------|-------------|------------|");
            foreach (var g in ctx.EndpointGroups)
            {
                sb.AppendLine($"| `{g.Signature.Normalized}` | {g.ObservationCount} | {g.FirstSeen:u} |");
            }
        }

        sb.AppendLine();

        if (ctx.Streams.Count > 0)
        {
            sb.AppendLine("## Streams Captured");
            sb.AppendLine();
            sb.AppendLine("| Protocol | URL | Started |");
            sb.AppendLine("|----------|-----|---------|");
            foreach (var s in ctx.Streams)
            {
                sb.AppendLine($"| {s.Protocol} | `{s.Url}` | {s.StartedAt:u} |");
            }
            sb.AppendLine();
        }

        if (ctx.SchemasByEndpoint.Count > 0)
        {
            sb.AppendLine("## Schema Inference Results");
            sb.AppendLine();
            foreach (var (endpoint, schema) in ctx.SchemasByEndpoint)
            {
                sb.AppendLine($"### `{endpoint}`");
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(schema.CSharpRecord);
                sb.AppendLine("```");
                sb.AppendLine();

                if (schema.Warnings.Count > 0)
                {
                    foreach (var w in schema.Warnings)
                        sb.AppendLine($"> Warning: {w}");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Export.Tests/ --filter "FullyQualifiedName~InvestigationNarrativeGeneratorTests" -v quiet`

```bash
git add src/Iaet.Export/Generators/InvestigationNarrativeGenerator.cs tests/Iaet.Export.Tests/Generators/InvestigationNarrativeGeneratorTests.cs
git commit -m "feat(export): implement InvestigationNarrativeGenerator"
```

---

## Task 6: CoverageReportGenerator

**Files:**
- Create: `src/Iaet.Export/Generators/CoverageReportGenerator.cs`
- Test: `tests/Iaet.Export.Tests/Generators/CoverageReportGeneratorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Export.Tests/Generators/CoverageReportGeneratorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class CoverageReportGeneratorTests
{
    [Fact]
    public void Generate_includes_observed_endpoints()
    {
        var ctx = TestContextFactory.MakeContext();

        var report = CoverageReportGenerator.Generate(ctx);

        report.Should().Contain("Coverage Report");
        report.Should().Contain("GET /api/users/{id}");
        report.Should().Contain("Observed");
    }

    [Fact]
    public void Generate_with_known_urls_shows_coverage_percentage()
    {
        var ctx = TestContextFactory.MakeContext();
        var knownUrls = new List<ExtractedUrl>
        {
            new() { Url = "/api/users/{id}", Confidence = ConfidenceLevel.High },
            new() { Url = "/api/unknown", Confidence = ConfidenceLevel.Low },
            new() { Url = "/api/secret", Confidence = ConfidenceLevel.Medium },
        };

        var report = CoverageReportGenerator.Generate(ctx, knownUrls);

        report.Should().Contain("Coverage:");
        report.Should().Contain("/api/unknown");
        report.Should().Contain("Not observed");
    }

    [Fact]
    public void Generate_handles_empty_context()
    {
        var ctx = TestContextFactory.MakeEmptyContext();

        var report = CoverageReportGenerator.Generate(ctx);

        report.Should().Contain("Coverage Report");
        report.Should().Contain("0 endpoints");
    }
}
```

- [ ] **Step 2: Add MakeEmptyContext to TestContextFactory**

Read `tests/Iaet.Export.Tests/TestContextFactory.cs` first, then add:

```csharp
public static ExportContext MakeEmptyContext() => new()
{
    Session = new CaptureSessionInfo
    {
        Id = Guid.NewGuid(),
        Name = "empty-session",
        TargetApplication = "EmptyApp",
        Profile = "default",
        StartedAt = DateTimeOffset.UtcNow,
    },
    Requests = [],
    EndpointGroups = [],
    Streams = [],
    SchemasByEndpoint = new Dictionary<string, SchemaResult>(),
};
```

- [ ] **Step 3: Implement CoverageReportGenerator**

```csharp
// src/Iaet.Export/Generators/CoverageReportGenerator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

public static class CoverageReportGenerator
{
    public static string Generate(ExportContext ctx, IReadOnlyList<ExtractedUrl>? knownUrls = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sb = new StringBuilder();
        sb.AppendLine("# Coverage Report");
        sb.AppendLine();
        sb.AppendLine($"**Target:** {ctx.Session.TargetApplication}");
        sb.AppendLine($"**Session:** {ctx.Session.Name}");
        sb.AppendLine();

        var observedSignatures = new HashSet<string>(
            ctx.EndpointGroups.Select(g => g.Signature.Normalized),
            StringComparer.OrdinalIgnoreCase);

        sb.AppendLine($"## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Observed endpoints:** {ctx.EndpointGroups.Count}");
        sb.AppendLine($"- **Total requests:** {ctx.Requests.Count}");
        sb.AppendLine($"- **Streams captured:** {ctx.Streams.Count}");
        sb.AppendLine($"- **Schemas inferred:** {ctx.SchemasByEndpoint.Count}");

        if (knownUrls is not null && knownUrls.Count > 0)
        {
            var matched = knownUrls.Count(k => observedSignatures.Any(s =>
                s.Contains(k.Url, StringComparison.OrdinalIgnoreCase)));
            var pct = knownUrls.Count > 0 ? (matched * 100) / knownUrls.Count : 0;

            sb.AppendLine($"- **Known endpoints:** {knownUrls.Count}");
            sb.AppendLine($"- **Coverage:** {pct}% ({matched}/{knownUrls.Count})");
        }

        sb.AppendLine();

        sb.AppendLine("## Observed Endpoints");
        sb.AppendLine();
        sb.AppendLine("| Endpoint | Status | Observations | Has Schema |");
        sb.AppendLine("|----------|--------|-------------|------------|");
        foreach (var g in ctx.EndpointGroups)
        {
            var hasSchema = ctx.SchemasByEndpoint.ContainsKey(g.Signature.Normalized) ? "Yes" : "No";
            sb.AppendLine($"| `{g.Signature.Normalized}` | Observed | {g.ObservationCount} | {hasSchema} |");
        }

        if (knownUrls is not null)
        {
            var unobserved = knownUrls.Where(k => !observedSignatures.Any(s =>
                s.Contains(k.Url, StringComparison.OrdinalIgnoreCase))).ToList();

            if (unobserved.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Not Observed (from JS analysis)");
                sb.AppendLine();
                sb.AppendLine("| URL | Confidence | Source |");
                sb.AppendLine("|-----|-----------|--------|");
                foreach (var u in unobserved)
                {
                    sb.AppendLine($"| `{u.Url}` | {u.Confidence} | {u.SourceFile ?? "unknown"} |");
                }
            }
        }

        sb.AppendLine();
        return sb.ToString();
    }

    public static string Generate(ExportContext ctx) => Generate(ctx, null);
}
```

- [ ] **Step 4: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Export.Tests/ --filter "FullyQualifiedName~CoverageReportGeneratorTests" -v quiet`

```bash
git add src/Iaet.Export/ tests/Iaet.Export.Tests/
git commit -m "feat(export): implement CoverageReportGenerator"
```

---

## Task 7: HumanActionItemsGenerator and SecretsAuditGenerator

**Files:**
- Create: `src/Iaet.Export/Generators/HumanActionItemsGenerator.cs`
- Create: `src/Iaet.Export/Generators/SecretsAuditGenerator.cs`
- Test: `tests/Iaet.Export.Tests/Generators/HumanActionItemsGeneratorTests.cs`
- Test: `tests/Iaet.Export.Tests/Generators/SecretsAuditGeneratorTests.cs`

- [ ] **Step 1: Write HumanActionItems tests**

```csharp
// tests/Iaet.Export.Tests/Generators/HumanActionItemsGeneratorTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class HumanActionItemsGeneratorTests
{
    [Fact]
    public void Generate_lists_action_items()
    {
        var items = new List<HumanActionRequest>
        {
            new() { Action = "Verify PRACK sequence manually", Reason = "Only 1 observation" },
            new() { Action = "Test SMS API with real message", Reason = "Needs live account" },
        };

        var markdown = HumanActionItemsGenerator.Generate("google-voice", items);

        markdown.Should().Contain("# Human Action Items");
        markdown.Should().Contain("Verify PRACK sequence manually");
        markdown.Should().Contain("Test SMS API with real message");
    }

    [Fact]
    public void Generate_shows_urgency()
    {
        var items = new List<HumanActionRequest>
        {
            new() { Action = "Re-authenticate", Reason = "Cookie expired", Urgency = "high" },
        };

        var markdown = HumanActionItemsGenerator.Generate("proj", items);

        markdown.Should().Contain("high");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var markdown = HumanActionItemsGenerator.Generate("proj", []);

        markdown.Should().Contain("No action items");
    }
}
```

- [ ] **Step 2: Write SecretsAudit tests**

```csharp
// tests/Iaet.Export.Tests/Generators/SecretsAuditGeneratorTests.cs
using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class SecretsAuditGeneratorTests
{
    [Fact]
    public void Generate_lists_secret_keys_without_values()
    {
        var secrets = new Dictionary<string, string>
        {
            ["SESSION_COOKIE"] = "super_secret_value_123",
            ["AUTH_TOKEN"] = "eyJhbGciOiJIUzI1NiJ9.payload.sig",
        };

        var markdown = SecretsAuditGenerator.Generate("google-voice", secrets);

        markdown.Should().Contain("# Secrets Audit");
        markdown.Should().Contain("SESSION_COOKIE");
        markdown.Should().Contain("AUTH_TOKEN");
        markdown.Should().NotContain("super_secret_value_123");
        markdown.Should().NotContain("eyJhbGciOiJIUzI1NiJ9");
    }

    [Fact]
    public void Generate_shows_value_lengths()
    {
        var secrets = new Dictionary<string, string>
        {
            ["SHORT_KEY"] = "abc",
            ["LONG_KEY"] = "a]very_long_secret_value_that_is_over_20_chars",
        };

        var markdown = SecretsAuditGenerator.Generate("proj", secrets);

        markdown.Should().Contain("3 chars");
        markdown.Should().Contain("46 chars");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var markdown = SecretsAuditGenerator.Generate("proj", new Dictionary<string, string>());

        markdown.Should().Contain("No secrets");
    }
}
```

- [ ] **Step 3: Implement HumanActionItemsGenerator**

```csharp
// src/Iaet.Export/Generators/HumanActionItemsGenerator.cs
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

public static class HumanActionItemsGenerator
{
    public static string Generate(string projectName, IReadOnlyList<HumanActionRequest> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var sb = new StringBuilder();
        sb.AppendLine("# Human Action Items");
        sb.AppendLine();
        sb.AppendLine($"**Project:** {projectName}");
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("No action items remaining.");
            return sb.ToString();
        }

        sb.AppendLine($"{items.Count} item(s) requiring human attention:");
        sb.AppendLine();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var urgencyTag = item.Urgency != "normal" ? $" [{item.Urgency}]" : "";
            sb.AppendLine($"### {i + 1}. {item.Action}{urgencyTag}");
            sb.AppendLine();
            sb.AppendLine($"**Reason:** {item.Reason}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Implement SecretsAuditGenerator**

```csharp
// src/Iaet.Export/Generators/SecretsAuditGenerator.cs
using System.Text;

namespace Iaet.Export.Generators;

public static class SecretsAuditGenerator
{
    public static string Generate(string projectName, IReadOnlyDictionary<string, string> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        var sb = new StringBuilder();
        sb.AppendLine("# Secrets Audit");
        sb.AppendLine();
        sb.AppendLine($"**Project:** {projectName}");
        sb.AppendLine();

        if (secrets.Count == 0)
        {
            sb.AppendLine("No secrets stored for this project.");
            return sb.ToString();
        }

        sb.AppendLine($"**Total secrets:** {secrets.Count}");
        sb.AppendLine();
        sb.AppendLine("> **Note:** Secret values are never included in this audit. Only key names and metadata are shown.");
        sb.AppendLine();

        sb.AppendLine("| Key | Length | Status |");
        sb.AppendLine("|-----|--------|--------|");
        foreach (var (key, value) in secrets.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"| `{key}` | {value.Length} chars | Active |");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
```

- [ ] **Step 5: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Export.Tests/ --filter "FullyQualifiedName~HumanActionItemsGeneratorTests|FullyQualifiedName~SecretsAuditGeneratorTests" -v quiet`

```bash
git add src/Iaet.Export/Generators/ tests/Iaet.Export.Tests/Generators/
git commit -m "feat(export): implement HumanActionItemsGenerator and SecretsAuditGenerator"
```

---

## Task 8: CLI Integration + Export Command Updates

**Files:**
- Modify: `src/Iaet.Cli/Iaet.Cli.csproj` — add Iaet.Diagrams reference
- Modify: `src/Iaet.Cli/Commands/ExportCommand.cs` — add narrative subcommand
- Modify: `src/Iaet.Cli/Program.cs` — register Iaet.Diagrams

- [ ] **Step 1: Add project reference**

Add to `src/Iaet.Cli/Iaet.Cli.csproj`:
```xml
<ProjectReference Include="..\Iaet.Diagrams\Iaet.Diagrams.csproj" />
```

- [ ] **Step 2: Update Program.cs**

Add `using Iaet.Diagrams;` and `services.AddIaetDiagrams();` in ConfigureServices.

- [ ] **Step 3: Add narrative export subcommand**

Read `src/Iaet.Cli/Commands/ExportCommand.cs` first, then add a new `CreateSubcommand` call for the narrative generator. Find where the other subcommands are registered and add:

```csharp
exportCmd.Add(CreateSubcommand("narrative", "Generate investigation narrative report",
    sessionIdOption, outputOption, services,
    ctx => InvestigationNarrativeGenerator.Generate(ctx), "Investigation narrative"));
```

Add `using Iaet.Export.Generators;` if not already present.

- [ ] **Step 4: Verify build and tests**

Run: `dotnet build Iaet.slnx && dotnet test Iaet.slnx -v quiet`

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cli/
git commit -m "feat(cli): add narrative export command and Iaet.Diagrams integration"
```

---

## Task 9: Full Integration Verification

- [ ] **Step 1: Run full solution build**

Run: `dotnet build Iaet.slnx`
Expected: 0 errors.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test Iaet.slnx -v quiet`
Expected: All tests pass.

- [ ] **Step 3: Verify new test counts**

Diagrams tests: ~12 (Sequence 4, DataFlow 3, DependencyGraph 3, StateMachine 2, Confidence 3)
Export tests: ~10 (Narrative 4, Coverage 3, HumanActions 3, SecretsAudit 3)
Total new: ~22

- [ ] **Step 4: Final commit if fixups needed**

```bash
git add -A
git commit -m "fix: integration fixups from Phase 4 smoke testing"
```

---

## Deferred Items

1. **Explorer enhancements** — Project dashboard, diagram viewer, auth status panel require Razor Pages changes and ASP.NET integration testing. Better suited as a standalone follow-up with its own testing infrastructure.

2. **Mermaid rendering in HTML reports** — Embedding client-side Mermaid.js in `HtmlReportGenerator` to render diagrams inline. Requires adding a JS library dependency.

3. **OpenAPI WebSocket documentation** — Extending `OpenApiGenerator` with AsyncAPI-style WebSocket endpoint documentation.
