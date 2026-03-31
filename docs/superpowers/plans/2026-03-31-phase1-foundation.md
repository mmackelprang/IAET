# Phase 1: Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the project model, secrets management, and agent orchestration framework that all other phases depend on.

**Architecture:** Three new assemblies (`Iaet.Projects`, `Iaet.Secrets`, `Iaet.Agents`) plus new domain models and abstractions in `Iaet.Core`. All follow IAET's dependency-inversion pattern — contracts live in `Iaet.Core`, implementations in their own assemblies. The CLI gets new command groups (`project`, `secrets`, `round`) and an enhanced `investigate --project` command.

**Tech Stack:** .NET 10, System.CommandLine v3, EF Core 10 + SQLite, xUnit + FluentAssertions + NSubstitute, Serilog

**Spec:** `docs/superpowers/specs/2026-03-31-agent-investigation-team-design.md`

---

## File Structure

### New files in existing assemblies

```
src/Iaet.Core/
  Models/
    ProjectConfig.cs          — project.json domain model
    ProjectStatus.cs          — enum: New, Investigating, Complete, Archived
    TargetType.cs             — enum: Web, Android, Desktop
    RoundPlan.cs              — what the Lead dispatches for a round
    AgentDispatch.cs          — single agent assignment within a round
    AgentFindings.cs          — structured results from one agent
    DiscoveredEndpoint.cs     — endpoint with confidence metadata
    ConfidenceLevel.cs        — enum: High, Medium, Low
    HumanActionRequest.cs     — pause request for human interaction
  Abstractions/
    IProjectStore.cs          — project CRUD
    ISecretsStore.cs          — secret read/write per project
    IKnowledgeStore.cs        — knowledge base read/write
    IRoundStore.cs            — round plan/findings persistence
    ISecretsRedactor.cs       — scrub secrets from strings
    IInvestigationAgent.cs    — agent execution contract
```

### New assembly: Iaet.Projects

```
src/Iaet.Projects/
  Iaet.Projects.csproj
  ProjectStore.cs             — filesystem-based IProjectStore
  RoundStore.cs               — filesystem-based IRoundStore
  KnowledgeStore.cs           — filesystem-based IKnowledgeStore
  ServiceCollectionExtensions.cs
tests/Iaet.Projects.Tests/
  Iaet.Projects.Tests.csproj
  ProjectStoreTests.cs
  RoundStoreTests.cs
  KnowledgeStoreTests.cs
```

### New assembly: Iaet.Secrets

```
src/Iaet.Secrets/
  Iaet.Secrets.csproj
  DotEnvSecretsStore.cs       — .env.iaet read/write
  SecretsRedactor.cs          — cross-ref scrubbing
  GitGuard.cs                 — gitignore validation
  ServiceCollectionExtensions.cs
tests/Iaet.Secrets.Tests/
  Iaet.Secrets.Tests.csproj
  DotEnvSecretsStoreTests.cs
  SecretsRedactorTests.cs
  GitGuardTests.cs
```

### New assembly: Iaet.Agents

```
src/Iaet.Agents/
  Iaet.Agents.csproj
  RoundExecutor.cs            — parallel agent dispatch + await
  FindingsMerger.cs           — dedup findings across agents
  HumanInteractionBroker.cs   — pause/resume for human actions
  InvestigationLog.cs         — append-only structured log
  ServiceCollectionExtensions.cs
tests/Iaet.Agents.Tests/
  Iaet.Agents.Tests.csproj
  RoundExecutorTests.cs
  FindingsMergerTests.cs
  HumanInteractionBrokerTests.cs
  InvestigationLogTests.cs
```

### Modified files

```
Iaet.slnx                    — add 6 new projects
src/Iaet.Cli/Iaet.Cli.csproj — add ProjectReference to new assemblies
src/Iaet.Cli/Program.cs      — register new services, add new commands
src/Iaet.Cli/Commands/
  ProjectCommand.cs           — iaet project create|list|status|archive
  SecretsCommand.cs           — iaet secrets set|get|list|audit
  RoundCommand.cs             — iaet round run|status
```

---

## Task 1: Solution Scaffolding

**Files:**
- Create: `src/Iaet.Projects/Iaet.Projects.csproj`
- Create: `src/Iaet.Secrets/Iaet.Secrets.csproj`
- Create: `src/Iaet.Agents/Iaet.Agents.csproj`
- Create: `tests/Iaet.Projects.Tests/Iaet.Projects.Tests.csproj`
- Create: `tests/Iaet.Secrets.Tests/Iaet.Secrets.Tests.csproj`
- Create: `tests/Iaet.Agents.Tests/Iaet.Agents.Tests.csproj`
- Modify: `Iaet.slnx`

- [ ] **Step 1: Create Iaet.Projects project file**

```xml
<!-- src/Iaet.Projects/Iaet.Projects.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Iaet.Secrets project file**

```xml
<!-- src/Iaet.Secrets/Iaet.Secrets.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Iaet.Agents project file**

```xml
<!-- src/Iaet.Agents/Iaet.Agents.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
    <ProjectReference Include="..\Iaet.Projects\Iaet.Projects.csproj" />
    <ProjectReference Include="..\Iaet.Secrets\Iaet.Secrets.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create test project files**

```xml
<!-- tests/Iaet.Projects.Tests/Iaet.Projects.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\Iaet.Projects\Iaet.Projects.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/Iaet.Secrets.Tests/Iaet.Secrets.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\Iaet.Secrets\Iaet.Secrets.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/Iaet.Agents.Tests/Iaet.Agents.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\Iaet.Agents\Iaet.Agents.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Projects\Iaet.Projects.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Secrets\Iaet.Secrets.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Update solution file**

Add to `Iaet.slnx` inside the `/src/` and `/tests/` folders:

```xml
<Solution>
  <Folder Name="/src/">
    <!-- existing entries -->
    <Project Path="src/Iaet.Projects/Iaet.Projects.csproj" />
    <Project Path="src/Iaet.Secrets/Iaet.Secrets.csproj" />
    <Project Path="src/Iaet.Agents/Iaet.Agents.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <!-- existing entries -->
    <Project Path="tests/Iaet.Projects.Tests/Iaet.Projects.Tests.csproj" />
    <Project Path="tests/Iaet.Secrets.Tests/Iaet.Secrets.Tests.csproj" />
    <Project Path="tests/Iaet.Agents.Tests/Iaet.Agents.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 6: Verify solution builds**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Projects/ src/Iaet.Secrets/ src/Iaet.Agents/ tests/Iaet.Projects.Tests/ tests/Iaet.Secrets.Tests/ tests/Iaet.Agents.Tests/ Iaet.slnx
git commit -m "chore: scaffold Iaet.Projects, Iaet.Secrets, Iaet.Agents assemblies"
```

---

## Task 2: Core Domain Models

**Files:**
- Create: `src/Iaet.Core/Models/ProjectConfig.cs`
- Create: `src/Iaet.Core/Models/ProjectStatus.cs`
- Create: `src/Iaet.Core/Models/TargetType.cs`
- Create: `src/Iaet.Core/Models/RoundPlan.cs`
- Create: `src/Iaet.Core/Models/AgentDispatch.cs`
- Create: `src/Iaet.Core/Models/AgentFindings.cs`
- Create: `src/Iaet.Core/Models/DiscoveredEndpoint.cs`
- Create: `src/Iaet.Core/Models/ConfidenceLevel.cs`
- Create: `src/Iaet.Core/Models/HumanActionRequest.cs`
- Test: `tests/Iaet.Core.Tests/Models/ProjectConfigTests.cs`

- [ ] **Step 1: Write tests for ProjectConfig**

```csharp
// tests/Iaet.Core.Tests/Models/ProjectConfigTests.cs
using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class ProjectConfigTests
{
    [Fact]
    public void Can_create_with_required_fields()
    {
        var config = new ProjectConfig
        {
            Name = "google-voice",
            DisplayName = "Google Voice Investigation",
            TargetType = TargetType.Web,
            EntryPoints = [new EntryPoint { Url = "https://voice.google.com", Label = "Main app" }],
        };

        config.Name.Should().Be("google-voice");
        config.Status.Should().Be(ProjectStatus.New);
        config.CurrentRound.Should().Be(0);
        config.AuthRequired.Should().BeFalse();
    }

    [Fact]
    public void Can_create_with_auth_and_focus_areas()
    {
        var config = new ProjectConfig
        {
            Name = "gv",
            DisplayName = "GV",
            TargetType = TargetType.Web,
            EntryPoints = [new EntryPoint { Url = "https://voice.google.com", Label = "Main" }],
            AuthRequired = true,
            AuthMethod = "browser-login",
            FocusAreas = ["call-signaling", "sms-api"],
        };

        config.AuthRequired.Should().BeTrue();
        config.AuthMethod.Should().Be("browser-login");
        config.FocusAreas.Should().HaveCount(2);
    }

    [Fact]
    public void RoundPlan_holds_dispatches_and_human_actions()
    {
        var plan = new RoundPlan
        {
            RoundNumber = 2,
            Rationale = "JS bundle found unobserved endpoints",
            Dispatches =
            [
                new AgentDispatch
                {
                    Agent = "js-bundle-analyzer",
                    Targets = ["https://voice.google.com/main-bundle.js"],
                },
            ],
            HumanActions =
            [
                new HumanActionRequest
                {
                    Action = "Place a phone call",
                    Reason = "Need call setup signaling",
                },
            ],
        };

        plan.Dispatches.Should().HaveCount(1);
        plan.HumanActions.Should().HaveCount(1);
    }

    [Fact]
    public void AgentFindings_carries_confidence_levels()
    {
        var findings = new AgentFindings
        {
            Agent = "network-capture",
            RoundNumber = 1,
            Endpoints =
            [
                new DiscoveredEndpoint
                {
                    Signature = "GET /api/v1/users",
                    Confidence = ConfidenceLevel.High,
                    ObservationCount = 5,
                    Sources = ["network-capture-round-1"],
                },
            ],
        };

        findings.Endpoints.Should().HaveCount(1);
        findings.Endpoints[0].Confidence.Should().Be(ConfidenceLevel.High);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Iaet.Core.Tests/ --filter "FullyQualifiedName~ProjectConfigTests" -v quiet`
Expected: Build error — types not defined yet.

- [ ] **Step 3: Create the enum types**

```csharp
// src/Iaet.Core/Models/ProjectStatus.cs
namespace Iaet.Core.Models;

public enum ProjectStatus
{
    New,
    Investigating,
    Complete,
    Archived,
}
```

```csharp
// src/Iaet.Core/Models/TargetType.cs
namespace Iaet.Core.Models;

public enum TargetType
{
    Web,
    Android,
    Desktop,
}
```

```csharp
// src/Iaet.Core/Models/ConfidenceLevel.cs
namespace Iaet.Core.Models;

public enum ConfidenceLevel
{
    High,
    Medium,
    Low,
}
```

- [ ] **Step 4: Create the record types**

```csharp
// src/Iaet.Core/Models/EntryPoint.cs
namespace Iaet.Core.Models;

public sealed record EntryPoint
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; init; }
    public required string Label { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/ProjectConfig.cs
namespace Iaet.Core.Models;

public sealed record ProjectConfig
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required TargetType TargetType { get; init; }
    public required IReadOnlyList<EntryPoint> EntryPoints { get; init; }
    public bool AuthRequired { get; init; }
    public string? AuthMethod { get; init; }
    public IReadOnlyList<string> FocusAreas { get; init; } = [];
    public CrawlConfig? CrawlConfig { get; init; }
    public int CurrentRound { get; init; }
    public ProjectStatus Status { get; init; } = ProjectStatus.New;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; init; } = DateTimeOffset.UtcNow;
}
```

```csharp
// src/Iaet.Core/Models/CrawlConfig.cs
namespace Iaet.Core.Models;

public sealed record CrawlConfig
{
    public int MaxDepth { get; init; } = 3;
    public int MaxPages { get; init; } = 50;
    public IReadOnlyList<string> Blacklist { get; init; } = [];
    public IReadOnlyList<string> ExcludeSelectors { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/HumanActionRequest.cs
namespace Iaet.Core.Models;

public sealed record HumanActionRequest
{
    public required string Action { get; init; }
    public required string Reason { get; init; }
    public string Urgency { get; init; } = "normal";
}
```

```csharp
// src/Iaet.Core/Models/AgentDispatch.cs
namespace Iaet.Core.Models;

public sealed record AgentDispatch
{
    public required string Agent { get; init; }
    public IReadOnlyList<string> Targets { get; init; } = [];
    public IReadOnlyList<string> Actions { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/RoundPlan.cs
namespace Iaet.Core.Models;

public sealed record RoundPlan
{
    public required int RoundNumber { get; init; }
    public required string Rationale { get; init; }
    public IReadOnlyList<AgentDispatch> Dispatches { get; init; } = [];
    public IReadOnlyList<HumanActionRequest> HumanActions { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/DiscoveredEndpoint.cs
namespace Iaet.Core.Models;

public sealed record DiscoveredEndpoint
{
    public required string Signature { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public int ObservationCount { get; init; }
    public IReadOnlyList<string> Sources { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/AgentFindings.cs
namespace Iaet.Core.Models;

public sealed record AgentFindings
{
    public required string Agent { get; init; }
    public required int RoundNumber { get; init; }
    public IReadOnlyList<DiscoveredEndpoint> Endpoints { get; init; } = [];
    public IReadOnlyList<string> GoDeeper { get; init; } = [];
    public IReadOnlyList<HumanActionRequest> HumanActions { get; init; } = [];
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Core.Tests/ --filter "FullyQualifiedName~ProjectConfigTests" -v quiet`
Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Core/Models/ tests/Iaet.Core.Tests/Models/ProjectConfigTests.cs
git commit -m "feat(core): add project, round, agent domain models"
```

---

## Task 3: Core Abstractions

**Files:**
- Create: `src/Iaet.Core/Abstractions/IProjectStore.cs`
- Create: `src/Iaet.Core/Abstractions/IRoundStore.cs`
- Create: `src/Iaet.Core/Abstractions/IKnowledgeStore.cs`
- Create: `src/Iaet.Core/Abstractions/ISecretsStore.cs`
- Create: `src/Iaet.Core/Abstractions/ISecretsRedactor.cs`
- Create: `src/Iaet.Core/Abstractions/IInvestigationAgent.cs`

- [ ] **Step 1: Create IProjectStore**

```csharp
// src/Iaet.Core/Abstractions/IProjectStore.cs
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IProjectStore
{
    Task<ProjectConfig> CreateAsync(ProjectConfig config, CancellationToken ct = default);
    Task<ProjectConfig?> LoadAsync(string projectName, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectConfig>> ListAsync(CancellationToken ct = default);
    Task SaveAsync(ProjectConfig config, CancellationToken ct = default);
    Task ArchiveAsync(string projectName, CancellationToken ct = default);
    string GetProjectDirectory(string projectName);
}
```

- [ ] **Step 2: Create IRoundStore**

```csharp
// src/Iaet.Core/Abstractions/IRoundStore.cs
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IRoundStore
{
    Task<int> CreateRoundAsync(string projectName, RoundPlan plan, CancellationToken ct = default);
    Task SaveFindingsAsync(string projectName, int roundNumber, AgentFindings findings, CancellationToken ct = default);
    Task<RoundPlan?> GetPlanAsync(string projectName, int roundNumber, CancellationToken ct = default);
    Task<IReadOnlyList<AgentFindings>> GetFindingsAsync(string projectName, int roundNumber, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create IKnowledgeStore**

```csharp
// src/Iaet.Core/Abstractions/IKnowledgeStore.cs
using System.Text.Json;

namespace Iaet.Core.Abstractions;

public interface IKnowledgeStore
{
    Task<JsonDocument?> ReadAsync(string projectName, string fileName, CancellationToken ct = default);
    Task WriteAsync(string projectName, string fileName, JsonDocument content, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListFilesAsync(string projectName, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create ISecretsStore and ISecretsRedactor**

```csharp
// src/Iaet.Core/Abstractions/ISecretsStore.cs
namespace Iaet.Core.Abstractions;

public interface ISecretsStore
{
    Task SetAsync(string projectName, string key, string value, CancellationToken ct = default);
    Task<string?> GetAsync(string projectName, string key, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> ListAsync(string projectName, CancellationToken ct = default);
    Task RemoveAsync(string projectName, string key, CancellationToken ct = default);
}
```

```csharp
// src/Iaet.Core/Abstractions/ISecretsRedactor.cs
namespace Iaet.Core.Abstractions;

public interface ISecretsRedactor
{
    string Redact(string input, string projectName);
    Task<string> RedactAsync(string input, string projectName, CancellationToken ct = default);
}
```

- [ ] **Step 5: Create IInvestigationAgent**

```csharp
// src/Iaet.Core/Abstractions/IInvestigationAgent.cs
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IInvestigationAgent
{
    string AgentName { get; }
    Task<AgentFindings> ExecuteAsync(AgentDispatch task, ProjectConfig project, CancellationToken ct = default);
}
```

- [ ] **Step 6: Verify build**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Core/Abstractions/
git commit -m "feat(core): add project, secrets, agent abstractions"
```

---

## Task 4: ProjectStore Implementation

**Files:**
- Create: `src/Iaet.Projects/ProjectStore.cs`
- Create: `src/Iaet.Projects/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Projects.Tests/ProjectStoreTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Projects.Tests/ProjectStoreTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Projects;

namespace Iaet.Projects.Tests;

public sealed class ProjectStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly ProjectStore _store;

    public ProjectStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _store = new ProjectStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Create_creates_directory_and_project_json()
    {
        var config = MakeConfig("test-project");

        var result = await _store.CreateAsync(config);

        result.Name.Should().Be("test-project");
        var dir = _store.GetProjectDirectory("test-project");
        Directory.Exists(dir).Should().BeTrue();
        File.Exists(Path.Combine(dir, "project.json")).Should().BeTrue();
    }

    [Fact]
    public async Task Create_creates_subdirectories()
    {
        await _store.CreateAsync(MakeConfig("test-project"));

        var dir = _store.GetProjectDirectory("test-project");
        Directory.Exists(Path.Combine(dir, "rounds")).Should().BeTrue();
        Directory.Exists(Path.Combine(dir, "output")).Should().BeTrue();
        Directory.Exists(Path.Combine(dir, "output", "diagrams")).Should().BeTrue();
        Directory.Exists(Path.Combine(dir, "knowledge")).Should().BeTrue();
    }

    [Fact]
    public async Task Load_returns_null_for_nonexistent()
    {
        var result = await _store.LoadAsync("nope");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Load_round_trips_config()
    {
        var config = MakeConfig("roundtrip");
        await _store.CreateAsync(config);

        var loaded = await _store.LoadAsync("roundtrip");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("roundtrip");
        loaded.TargetType.Should().Be(TargetType.Web);
        loaded.EntryPoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_returns_all_projects()
    {
        await _store.CreateAsync(MakeConfig("alpha"));
        await _store.CreateAsync(MakeConfig("beta"));

        var list = await _store.ListAsync();

        list.Should().HaveCount(2);
        list.Select(p => p.Name).Should().Contain(["alpha", "beta"]);
    }

    [Fact]
    public async Task Save_updates_existing_config()
    {
        var config = MakeConfig("updatable");
        await _store.CreateAsync(config);

        var updated = config with { Status = ProjectStatus.Investigating, CurrentRound = 2 };
        await _store.SaveAsync(updated);

        var loaded = await _store.LoadAsync("updatable");
        loaded!.Status.Should().Be(ProjectStatus.Investigating);
        loaded.CurrentRound.Should().Be(2);
    }

    [Fact]
    public async Task Archive_sets_status_to_archived()
    {
        await _store.CreateAsync(MakeConfig("archivable"));

        await _store.ArchiveAsync("archivable");

        var loaded = await _store.LoadAsync("archivable");
        loaded!.Status.Should().Be(ProjectStatus.Archived);
    }

    private static ProjectConfig MakeConfig(string name) => new()
    {
        Name = name,
        DisplayName = $"Test {name}",
        TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Projects.Tests/ --filter "FullyQualifiedName~ProjectStoreTests" -v quiet`
Expected: Build error — `ProjectStore` not defined.

- [ ] **Step 3: Implement ProjectStore**

```csharp
// src/Iaet.Projects/ProjectStore.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Projects;

public sealed class ProjectStore(string rootDirectory) : IProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string GetProjectDirectory(string projectName) =>
        Path.Combine(rootDirectory, projectName);

    public async Task<ProjectConfig> CreateAsync(ProjectConfig config, CancellationToken ct = default)
    {
        var dir = GetProjectDirectory(config.Name);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "rounds"));
        Directory.CreateDirectory(Path.Combine(dir, "output", "diagrams"));
        Directory.CreateDirectory(Path.Combine(dir, "knowledge"));

        await WriteConfigAsync(dir, config, ct).ConfigureAwait(false);
        return config;
    }

    public async Task<ProjectConfig?> LoadAsync(string projectName, CancellationToken ct = default)
    {
        var configPath = Path.Combine(GetProjectDirectory(projectName), "project.json");
        if (!File.Exists(configPath))
            return null;

        var json = await File.ReadAllTextAsync(configPath, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ProjectConfig>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<ProjectConfig>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(rootDirectory))
            return [];

        var results = new List<ProjectConfig>();
        foreach (var dir in Directory.GetDirectories(rootDirectory))
        {
            var name = Path.GetFileName(dir);
            var config = await LoadAsync(name, ct).ConfigureAwait(false);
            if (config is not null)
                results.Add(config);
        }
        return results;
    }

    public async Task SaveAsync(ProjectConfig config, CancellationToken ct = default)
    {
        var dir = GetProjectDirectory(config.Name);
        await WriteConfigAsync(dir, config, ct).ConfigureAwait(false);
    }

    public async Task ArchiveAsync(string projectName, CancellationToken ct = default)
    {
        var config = await LoadAsync(projectName, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Project '{projectName}' not found.");
        var archived = config with { Status = ProjectStatus.Archived };
        await SaveAsync(archived, ct).ConfigureAwait(false);
    }

    private static async Task WriteConfigAsync(string dir, ProjectConfig config, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(dir, "project.json"), json, ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.Projects/ServiceCollectionExtensions.cs
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Projects;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetProjects(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton<IProjectStore>(new ProjectStore(rootDirectory));
        return services;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Projects.Tests/ --filter "FullyQualifiedName~ProjectStoreTests" -v quiet`
Expected: All 7 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Projects/ tests/Iaet.Projects.Tests/ProjectStoreTests.cs
git commit -m "feat(projects): implement filesystem-based ProjectStore"
```

---

## Task 5: RoundStore Implementation

**Files:**
- Create: `src/Iaet.Projects/RoundStore.cs`
- Modify: `src/Iaet.Projects/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Projects.Tests/RoundStoreTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Projects.Tests/RoundStoreTests.cs
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Projects;

namespace Iaet.Projects.Tests;

public sealed class RoundStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly ProjectStore _projectStore;
    private readonly RoundStore _store;

    public RoundStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _projectStore = new ProjectStore(_rootDir);
        _store = new RoundStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task CreateRound_creates_numbered_directory_and_plan()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        var plan = new RoundPlan
        {
            RoundNumber = 1,
            Rationale = "Initial capture",
            Dispatches = [new AgentDispatch { Agent = "network-capture", Targets = ["https://example.com"] }],
        };

        var roundNum = await _store.CreateRoundAsync("proj", plan);

        roundNum.Should().Be(1);
        var roundDir = Path.Combine(_rootDir, "proj", "rounds", "001-round");
        Directory.Exists(roundDir).Should().BeTrue();
        File.Exists(Path.Combine(roundDir, "plan.json")).Should().BeTrue();
    }

    [Fact]
    public async Task GetPlan_round_trips()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        var plan = new RoundPlan
        {
            RoundNumber = 1,
            Rationale = "Test rationale",
        };
        await _store.CreateRoundAsync("proj", plan);

        var loaded = await _store.GetPlanAsync("proj", 1);

        loaded.Should().NotBeNull();
        loaded!.Rationale.Should().Be("Test rationale");
    }

    [Fact]
    public async Task SaveFindings_and_GetFindings_round_trip()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        await _store.CreateRoundAsync("proj", new RoundPlan { RoundNumber = 1, Rationale = "test" });

        var findings = new AgentFindings
        {
            Agent = "network-capture",
            RoundNumber = 1,
            Endpoints = [new DiscoveredEndpoint { Signature = "GET /api", Confidence = ConfidenceLevel.High, ObservationCount = 3 }],
        };
        await _store.SaveFindingsAsync("proj", 1, findings);

        var loaded = await _store.GetFindingsAsync("proj", 1);
        loaded.Should().HaveCount(1);
        loaded[0].Agent.Should().Be("network-capture");
        loaded[0].Endpoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveFindings_accumulates_multiple_agents()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        await _store.CreateRoundAsync("proj", new RoundPlan { RoundNumber = 1, Rationale = "test" });

        await _store.SaveFindingsAsync("proj", 1, new AgentFindings { Agent = "agent-a", RoundNumber = 1 });
        await _store.SaveFindingsAsync("proj", 1, new AgentFindings { Agent = "agent-b", RoundNumber = 1 });

        var loaded = await _store.GetFindingsAsync("proj", 1);
        loaded.Should().HaveCount(2);
    }

    private static ProjectConfig MakeConfig(string name) => new()
    {
        Name = name,
        DisplayName = name,
        TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Projects.Tests/ --filter "FullyQualifiedName~RoundStoreTests" -v quiet`
Expected: Build error — `RoundStore` not defined.

- [ ] **Step 3: Implement RoundStore**

```csharp
// src/Iaet.Projects/RoundStore.cs
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Projects;

public sealed class RoundStore(string rootDirectory) : IRoundStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<int> CreateRoundAsync(string projectName, RoundPlan plan, CancellationToken ct = default)
    {
        var roundDir = GetRoundDirectory(projectName, plan.RoundNumber);
        Directory.CreateDirectory(roundDir);

        var json = JsonSerializer.Serialize(plan, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(roundDir, "plan.json"), json, ct).ConfigureAwait(false);

        return plan.RoundNumber;
    }

    public async Task SaveFindingsAsync(string projectName, int roundNumber, AgentFindings findings, CancellationToken ct = default)
    {
        var roundDir = GetRoundDirectory(projectName, roundNumber);
        Directory.CreateDirectory(roundDir);

        var fileName = $"findings-{findings.Agent}.json";
        var json = JsonSerializer.Serialize(findings, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(roundDir, fileName), json, ct).ConfigureAwait(false);
    }

    public async Task<RoundPlan?> GetPlanAsync(string projectName, int roundNumber, CancellationToken ct = default)
    {
        var planPath = Path.Combine(GetRoundDirectory(projectName, roundNumber), "plan.json");
        if (!File.Exists(planPath))
            return null;

        var json = await File.ReadAllTextAsync(planPath, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<RoundPlan>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<AgentFindings>> GetFindingsAsync(string projectName, int roundNumber, CancellationToken ct = default)
    {
        var roundDir = GetRoundDirectory(projectName, roundNumber);
        if (!Directory.Exists(roundDir))
            return [];

        var results = new List<AgentFindings>();
        foreach (var file in Directory.GetFiles(roundDir, "findings-*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var findings = JsonSerializer.Deserialize<AgentFindings>(json, JsonOptions);
            if (findings is not null)
                results.Add(findings);
        }
        return results;
    }

    private string GetRoundDirectory(string projectName, int roundNumber)
    {
        var roundLabel = roundNumber.ToString("D3", CultureInfo.InvariantCulture) + "-round";
        return Path.Combine(rootDirectory, projectName, "rounds", roundLabel);
    }
}
```

- [ ] **Step 4: Register in DI**

Add to `src/Iaet.Projects/ServiceCollectionExtensions.cs`:

```csharp
// Add after the IProjectStore registration line:
services.AddSingleton<IRoundStore>(new RoundStore(rootDirectory));
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Projects.Tests/ --filter "FullyQualifiedName~RoundStoreTests" -v quiet`
Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Projects/ tests/Iaet.Projects.Tests/RoundStoreTests.cs
git commit -m "feat(projects): implement RoundStore for plan/findings persistence"
```

---

## Task 6: KnowledgeStore Implementation

**Files:**
- Create: `src/Iaet.Projects/KnowledgeStore.cs`
- Modify: `src/Iaet.Projects/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Projects.Tests/KnowledgeStoreTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Projects.Tests/KnowledgeStoreTests.cs
using System.Text.Json;
using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Projects;

namespace Iaet.Projects.Tests;

public sealed class KnowledgeStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly ProjectStore _projectStore;
    private readonly KnowledgeStore _store;

    public KnowledgeStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _projectStore = new ProjectStore(_rootDir);
        _store = new KnowledgeStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Read_returns_null_for_nonexistent()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        var result = await _store.ReadAsync("proj", "endpoints.json");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Write_and_Read_round_trip()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        using var doc = JsonDocument.Parse("""{"endpoints": [{"name": "GET /api"}]}""");
        await _store.WriteAsync("proj", "endpoints.json", doc);

        using var loaded = await _store.ReadAsync("proj", "endpoints.json");
        loaded.Should().NotBeNull();
        loaded!.RootElement.GetProperty("endpoints").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ListFiles_returns_written_files()
    {
        await _projectStore.CreateAsync(MakeConfig("proj"));
        using var doc = JsonDocument.Parse("{}");
        await _store.WriteAsync("proj", "endpoints.json", doc);
        await _store.WriteAsync("proj", "cookies.json", doc);

        var files = await _store.ListFilesAsync("proj");
        files.Should().Contain(["endpoints.json", "cookies.json"]);
    }

    private static ProjectConfig MakeConfig(string name) => new()
    {
        Name = name,
        DisplayName = name,
        TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Projects.Tests/ --filter "FullyQualifiedName~KnowledgeStoreTests" -v quiet`
Expected: Build error — `KnowledgeStore` not defined.

- [ ] **Step 3: Implement KnowledgeStore**

```csharp
// src/Iaet.Projects/KnowledgeStore.cs
using System.Text.Json;
using Iaet.Core.Abstractions;

namespace Iaet.Projects;

public sealed class KnowledgeStore(string rootDirectory) : IKnowledgeStore
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public async Task<JsonDocument?> ReadAsync(string projectName, string fileName, CancellationToken ct = default)
    {
        var path = GetPath(projectName, fileName);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }

    public async Task WriteAsync(string projectName, string fileName, JsonDocument content, CancellationToken ct = default)
    {
        var path = GetPath(projectName, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        content.WriteTo(writer);
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string projectName, CancellationToken ct = default)
    {
        var dir = Path.Combine(rootDirectory, projectName, "knowledge");
        if (!Directory.Exists(dir))
            return Task.FromResult<IReadOnlyList<string>>([]);

        var files = Directory.GetFiles(dir, "*.json")
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .Cast<string>()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private string GetPath(string projectName, string fileName) =>
        Path.Combine(rootDirectory, projectName, "knowledge", fileName);
}
```

- [ ] **Step 4: Register in DI**

Add to `src/Iaet.Projects/ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IKnowledgeStore>(new KnowledgeStore(rootDirectory));
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Projects.Tests/ --filter "FullyQualifiedName~KnowledgeStoreTests" -v quiet`
Expected: All 3 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Projects/ tests/Iaet.Projects.Tests/KnowledgeStoreTests.cs
git commit -m "feat(projects): implement KnowledgeStore for structured JSON knowledge base"
```

---

## Task 7: DotEnvSecretsStore Implementation

**Files:**
- Create: `src/Iaet.Secrets/DotEnvSecretsStore.cs`
- Create: `src/Iaet.Secrets/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Secrets.Tests/DotEnvSecretsStoreTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Secrets.Tests/DotEnvSecretsStoreTests.cs
using FluentAssertions;
using Iaet.Secrets;

namespace Iaet.Secrets.Tests;

public sealed class DotEnvSecretsStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly DotEnvSecretsStore _store;

    public DotEnvSecretsStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_rootDir, "proj"));
        _store = new DotEnvSecretsStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Get_returns_null_when_no_env_file()
    {
        var result = await _store.GetAsync("proj", "MY_KEY");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Set_and_Get_round_trip()
    {
        await _store.SetAsync("proj", "MY_TOKEN", "secret123");
        var result = await _store.GetAsync("proj", "MY_TOKEN");
        result.Should().Be("secret123");
    }

    [Fact]
    public async Task Set_overwrites_existing_key()
    {
        await _store.SetAsync("proj", "KEY", "old");
        await _store.SetAsync("proj", "KEY", "new");
        var result = await _store.GetAsync("proj", "KEY");
        result.Should().Be("new");
    }

    [Fact]
    public async Task List_returns_all_keys()
    {
        await _store.SetAsync("proj", "A", "1");
        await _store.SetAsync("proj", "B", "2");

        var all = await _store.ListAsync("proj");
        all.Should().HaveCount(2);
        all["A"].Should().Be("1");
        all["B"].Should().Be("2");
    }

    [Fact]
    public async Task Remove_deletes_key()
    {
        await _store.SetAsync("proj", "REMOVE_ME", "val");
        await _store.RemoveAsync("proj", "REMOVE_ME");

        var result = await _store.GetAsync("proj", "REMOVE_ME");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Ignores_comments_and_blank_lines()
    {
        var envPath = Path.Combine(_rootDir, "proj", ".env.iaet");
        await File.WriteAllTextAsync(envPath, "# comment\n\nKEY=value\n");

        var result = await _store.GetAsync("proj", "KEY");
        result.Should().Be("value");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Secrets.Tests/ --filter "FullyQualifiedName~DotEnvSecretsStoreTests" -v quiet`
Expected: Build error — `DotEnvSecretsStore` not defined.

- [ ] **Step 3: Implement DotEnvSecretsStore**

```csharp
// src/Iaet.Secrets/DotEnvSecretsStore.cs
using Iaet.Core.Abstractions;

namespace Iaet.Secrets;

public sealed class DotEnvSecretsStore(string rootDirectory) : ISecretsStore
{
    private const string FileName = ".env.iaet";

    public async Task SetAsync(string projectName, string key, string value, CancellationToken ct = default)
    {
        var entries = await ReadEntriesAsync(projectName, ct).ConfigureAwait(false);
        entries[key] = value;
        await WriteEntriesAsync(projectName, entries, ct).ConfigureAwait(false);
    }

    public async Task<string?> GetAsync(string projectName, string key, CancellationToken ct = default)
    {
        var entries = await ReadEntriesAsync(projectName, ct).ConfigureAwait(false);
        return entries.TryGetValue(key, out var value) ? value : null;
    }

    public async Task<IReadOnlyDictionary<string, string>> ListAsync(string projectName, CancellationToken ct = default)
    {
        return await ReadEntriesAsync(projectName, ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string projectName, string key, CancellationToken ct = default)
    {
        var entries = await ReadEntriesAsync(projectName, ct).ConfigureAwait(false);
        entries.Remove(key);
        await WriteEntriesAsync(projectName, entries, ct).ConfigureAwait(false);
    }

    private string GetEnvPath(string projectName) =>
        Path.Combine(rootDirectory, projectName, FileName);

    private async Task<Dictionary<string, string>> ReadEntriesAsync(string projectName, CancellationToken ct)
    {
        var path = GetEnvPath(projectName);
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var eqIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (eqIndex <= 0)
                continue;

            var k = trimmed[..eqIndex].Trim();
            var v = trimmed[(eqIndex + 1)..].Trim();
            entries[k] = v;
        }
        return entries;
    }

    private async Task WriteEntriesAsync(string projectName, Dictionary<string, string> entries, CancellationToken ct)
    {
        var path = GetEnvPath(projectName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var lines = new List<string>
        {
            $"# IAET secrets for project: {projectName}",
            "# Auto-generated — do not commit",
            "",
        };

        foreach (var (key, value) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            lines.Add($"{key}={value}");
        }

        await File.WriteAllLinesAsync(path, lines, ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.Secrets/ServiceCollectionExtensions.cs
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Secrets;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetSecrets(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton<ISecretsStore>(new DotEnvSecretsStore(rootDirectory));
        return services;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Secrets.Tests/ --filter "FullyQualifiedName~DotEnvSecretsStoreTests" -v quiet`
Expected: All 6 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Secrets/ tests/Iaet.Secrets.Tests/DotEnvSecretsStoreTests.cs
git commit -m "feat(secrets): implement DotEnvSecretsStore for project-scoped .env.iaet"
```

---

## Task 8: SecretsRedactor Implementation

**Files:**
- Create: `src/Iaet.Secrets/SecretsRedactor.cs`
- Modify: `src/Iaet.Secrets/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Secrets.Tests/SecretsRedactorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Secrets.Tests/SecretsRedactorTests.cs
using FluentAssertions;
using Iaet.Secrets;

namespace Iaet.Secrets.Tests;

public sealed class SecretsRedactorTests : IDisposable
{
    private readonly string _rootDir;
    private readonly DotEnvSecretsStore _secretsStore;
    private readonly SecretsRedactor _redactor;

    public SecretsRedactorTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_rootDir, "proj"));
        _secretsStore = new DotEnvSecretsStore(_rootDir);
        _redactor = new SecretsRedactor(_secretsStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Redact_replaces_secret_values_with_marker()
    {
        await _secretsStore.SetAsync("proj", "TOKEN", "abc123secret");

        var result = await _redactor.RedactAsync("Authorization: Bearer abc123secret", "proj");

        result.Should().Be("Authorization: Bearer <REDACTED:TOKEN>");
    }

    [Fact]
    public async Task Redact_handles_multiple_secrets()
    {
        await _secretsStore.SetAsync("proj", "TOKEN_A", "secret1");
        await _secretsStore.SetAsync("proj", "TOKEN_B", "secret2");

        var result = await _redactor.RedactAsync("secret1 and secret2", "proj");

        result.Should().NotContain("secret1");
        result.Should().NotContain("secret2");
    }

    [Fact]
    public async Task Redact_returns_input_unchanged_when_no_secrets()
    {
        var result = await _redactor.RedactAsync("nothing to redact", "proj");
        result.Should().Be("nothing to redact");
    }

    [Fact]
    public async Task Redact_skips_short_values()
    {
        await _secretsStore.SetAsync("proj", "SHORT", "ab");

        var result = await _redactor.RedactAsync("ab is fine", "proj");

        result.Should().Be("ab is fine");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Secrets.Tests/ --filter "FullyQualifiedName~SecretsRedactorTests" -v quiet`
Expected: Build error — `SecretsRedactor` not defined.

- [ ] **Step 3: Implement SecretsRedactor**

```csharp
// src/Iaet.Secrets/SecretsRedactor.cs
using Iaet.Core.Abstractions;

namespace Iaet.Secrets;

public sealed class SecretsRedactor(ISecretsStore secretsStore) : ISecretsRedactor
{
    private const int MinSecretLength = 4;

    public string Redact(string input, string projectName)
    {
        return RedactAsync(input, projectName).GetAwaiter().GetResult();
    }

    public async Task<string> RedactAsync(string input, string projectName, CancellationToken ct = default)
    {
        var secrets = await secretsStore.ListAsync(projectName, ct).ConfigureAwait(false);

        var result = input;
        foreach (var (key, value) in secrets.OrderByDescending(s => s.Value.Length))
        {
            if (value.Length < MinSecretLength)
                continue;

            result = result.Replace(value, $"<REDACTED:{key}>", StringComparison.Ordinal);
        }
        return result;
    }
}
```

- [ ] **Step 4: Register in DI**

Add to `src/Iaet.Secrets/ServiceCollectionExtensions.cs` inside the method:

```csharp
services.AddSingleton<ISecretsRedactor, SecretsRedactor>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Secrets.Tests/ --filter "FullyQualifiedName~SecretsRedactorTests" -v quiet`
Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Secrets/ tests/Iaet.Secrets.Tests/SecretsRedactorTests.cs
git commit -m "feat(secrets): implement SecretsRedactor for credential scrubbing"
```

---

## Task 9: GitGuard Implementation

**Files:**
- Create: `src/Iaet.Secrets/GitGuard.cs`
- Test: `tests/Iaet.Secrets.Tests/GitGuardTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Secrets.Tests/GitGuardTests.cs
using FluentAssertions;
using Iaet.Secrets;

namespace Iaet.Secrets.Tests;

public sealed class GitGuardTests : IDisposable
{
    private readonly string _repoDir;

    public GitGuardTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_repoDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoDir))
            Directory.Delete(_repoDir, recursive: true);
    }

    [Fact]
    public void EnsureGitignore_creates_file_with_env_pattern()
    {
        GitGuard.EnsureGitignore(_repoDir);

        var gitignorePath = Path.Combine(_repoDir, ".gitignore");
        File.Exists(gitignorePath).Should().BeTrue();
        var content = File.ReadAllText(gitignorePath);
        content.Should().Contain(".env.iaet");
    }

    [Fact]
    public void EnsureGitignore_appends_to_existing_file()
    {
        var gitignorePath = Path.Combine(_repoDir, ".gitignore");
        File.WriteAllText(gitignorePath, "node_modules/\n");

        GitGuard.EnsureGitignore(_repoDir);

        var content = File.ReadAllText(gitignorePath);
        content.Should().Contain("node_modules/");
        content.Should().Contain(".env.iaet");
    }

    [Fact]
    public void EnsureGitignore_is_idempotent()
    {
        GitGuard.EnsureGitignore(_repoDir);
        GitGuard.EnsureGitignore(_repoDir);

        var content = File.ReadAllText(Path.Combine(_repoDir, ".gitignore"));
        var count = content.Split(".env.iaet").Length - 1;
        count.Should().Be(1);
    }

    [Fact]
    public void EnsureGitignore_includes_projects_directory()
    {
        GitGuard.EnsureGitignore(_repoDir);

        var content = File.ReadAllText(Path.Combine(_repoDir, ".gitignore"));
        content.Should().Contain(".iaet-projects/**/.env.iaet");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Secrets.Tests/ --filter "FullyQualifiedName~GitGuardTests" -v quiet`
Expected: Build error — `GitGuard` not defined.

- [ ] **Step 3: Implement GitGuard**

```csharp
// src/Iaet.Secrets/GitGuard.cs
namespace Iaet.Secrets;

public static class GitGuard
{
    private static readonly string[] RequiredPatterns =
    [
        ".iaet-projects/**/.env.iaet",
    ];

    public static void EnsureGitignore(string repoRoot)
    {
        var gitignorePath = Path.Combine(repoRoot, ".gitignore");
        var existingContent = File.Exists(gitignorePath)
            ? File.ReadAllText(gitignorePath)
            : string.Empty;

        var linesToAdd = new List<string>();
        foreach (var pattern in RequiredPatterns)
        {
            if (!existingContent.Contains(pattern, StringComparison.Ordinal))
                linesToAdd.Add(pattern);
        }

        if (linesToAdd.Count == 0)
            return;

        using var writer = File.AppendText(gitignorePath);
        if (existingContent.Length > 0 && !existingContent.EndsWith('\n'))
            writer.WriteLine();

        writer.WriteLine();
        writer.WriteLine("# IAET secrets — never commit");
        foreach (var line in linesToAdd)
        {
            writer.WriteLine(line);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Secrets.Tests/ --filter "FullyQualifiedName~GitGuardTests" -v quiet`
Expected: All 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Secrets/GitGuard.cs tests/Iaet.Secrets.Tests/GitGuardTests.cs
git commit -m "feat(secrets): implement GitGuard for .gitignore enforcement"
```

---

## Task 10: InvestigationLog Implementation

**Files:**
- Create: `src/Iaet.Agents/InvestigationLog.cs`
- Create: `src/Iaet.Agents/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Agents.Tests/InvestigationLogTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Agents.Tests/InvestigationLogTests.cs
using FluentAssertions;
using Iaet.Agents;

namespace Iaet.Agents.Tests;

public sealed class InvestigationLogTests : IDisposable
{
    private readonly string _rootDir;
    private readonly InvestigationLog _log;

    public InvestigationLogTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_rootDir, "proj"));
        _log = new InvestigationLog(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Append_creates_log_file()
    {
        await _log.AppendAsync("proj", "lead", "Started investigation");

        var logPath = Path.Combine(_rootDir, "proj", "investigation.log");
        File.Exists(logPath).Should().BeTrue();
    }

    [Fact]
    public async Task Append_writes_structured_entries()
    {
        await _log.AppendAsync("proj", "lead", "Round 1 started");
        await _log.AppendAsync("proj", "network-capture", "Found 12 endpoints");

        var lines = await File.ReadAllLinesAsync(
            Path.Combine(_rootDir, "proj", "investigation.log"));
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("[lead]");
        lines[0].Should().Contain("Round 1 started");
        lines[1].Should().Contain("[network-capture]");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~InvestigationLogTests" -v quiet`
Expected: Build error — `InvestigationLog` not defined.

- [ ] **Step 3: Implement InvestigationLog**

```csharp
// src/Iaet.Agents/InvestigationLog.cs
using System.Globalization;

namespace Iaet.Agents;

public sealed class InvestigationLog(string rootDirectory)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task AppendAsync(string projectName, string agent, string message, CancellationToken ct = default)
    {
        var logPath = Path.Combine(rootDirectory, projectName, "investigation.log");
        var timestamp = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var line = $"{timestamp} [{agent}] {message}";

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllLinesAsync(logPath, [line], ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

- [ ] **Step 4: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.Agents/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Agents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetAgents(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton(new InvestigationLog(rootDirectory));
        return services;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~InvestigationLogTests" -v quiet`
Expected: All 2 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Agents/ tests/Iaet.Agents.Tests/InvestigationLogTests.cs
git commit -m "feat(agents): implement InvestigationLog for append-only audit trail"
```

---

## Task 11: FindingsMerger Implementation

**Files:**
- Create: `src/Iaet.Agents/FindingsMerger.cs`
- Test: `tests/Iaet.Agents.Tests/FindingsMergerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Agents.Tests/FindingsMergerTests.cs
using FluentAssertions;
using Iaet.Agents;
using Iaet.Core.Models;

namespace Iaet.Agents.Tests;

public sealed class FindingsMergerTests
{
    [Fact]
    public void Merge_combines_endpoints_from_multiple_agents()
    {
        var findingsA = new AgentFindings
        {
            Agent = "agent-a",
            RoundNumber = 1,
            Endpoints =
            [
                new DiscoveredEndpoint { Signature = "GET /api/users", Confidence = ConfidenceLevel.High, ObservationCount = 3, Sources = ["agent-a"] },
            ],
        };
        var findingsB = new AgentFindings
        {
            Agent = "agent-b",
            RoundNumber = 1,
            Endpoints =
            [
                new DiscoveredEndpoint { Signature = "POST /api/login", Confidence = ConfidenceLevel.Medium, ObservationCount = 1, Sources = ["agent-b"] },
            ],
        };

        var merged = FindingsMerger.Merge([findingsA, findingsB]);

        merged.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_deduplicates_same_endpoint_keeping_highest_confidence()
    {
        var findingsA = new AgentFindings
        {
            Agent = "agent-a",
            RoundNumber = 1,
            Endpoints =
            [
                new DiscoveredEndpoint { Signature = "GET /api/users", Confidence = ConfidenceLevel.Low, ObservationCount = 1, Sources = ["agent-a"] },
            ],
        };
        var findingsB = new AgentFindings
        {
            Agent = "agent-b",
            RoundNumber = 1,
            Endpoints =
            [
                new DiscoveredEndpoint { Signature = "GET /api/users", Confidence = ConfidenceLevel.High, ObservationCount = 5, Sources = ["agent-b"] },
            ],
        };

        var merged = FindingsMerger.Merge([findingsA, findingsB]);

        merged.Should().HaveCount(1);
        merged[0].Confidence.Should().Be(ConfidenceLevel.High);
        merged[0].ObservationCount.Should().Be(6);
        merged[0].Sources.Should().Contain(["agent-a", "agent-b"]);
    }

    [Fact]
    public void Merge_handles_empty_input()
    {
        var merged = FindingsMerger.Merge([]);
        merged.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~FindingsMergerTests" -v quiet`
Expected: Build error — `FindingsMerger` not defined.

- [ ] **Step 3: Implement FindingsMerger**

```csharp
// src/Iaet.Agents/FindingsMerger.cs
using Iaet.Core.Models;

namespace Iaet.Agents;

public static class FindingsMerger
{
    public static IReadOnlyList<DiscoveredEndpoint> Merge(IReadOnlyList<AgentFindings> allFindings)
    {
        var bySignature = new Dictionary<string, MergeState>(StringComparer.Ordinal);

        foreach (var findings in allFindings)
        {
            foreach (var ep in findings.Endpoints)
            {
                if (!bySignature.TryGetValue(ep.Signature, out var state))
                {
                    state = new MergeState(ep.Signature);
                    bySignature[ep.Signature] = state;
                }

                state.ObservationCount += ep.ObservationCount;
                state.Sources.AddRange(ep.Sources);
                state.Limitations.AddRange(ep.Limitations);

                if (ep.Confidence < state.BestConfidence)
                    state.BestConfidence = ep.Confidence;
            }
        }

        return bySignature.Values
            .Select(s => new DiscoveredEndpoint
            {
                Signature = s.Signature,
                Confidence = s.BestConfidence,
                ObservationCount = s.ObservationCount,
                Sources = s.Sources.Distinct().ToList(),
                Limitations = s.Limitations.Distinct().ToList(),
            })
            .ToList();
    }

    private sealed class MergeState(string signature)
    {
        public string Signature { get; } = signature;
        public ConfidenceLevel BestConfidence { get; set; } = ConfidenceLevel.Low;
        public int ObservationCount { get; set; }
        public List<string> Sources { get; } = [];
        public List<string> Limitations { get; } = [];
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~FindingsMergerTests" -v quiet`
Expected: All 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Agents/FindingsMerger.cs tests/Iaet.Agents.Tests/FindingsMergerTests.cs
git commit -m "feat(agents): implement FindingsMerger for cross-agent deduplication"
```

---

## Task 12: HumanInteractionBroker Implementation

**Files:**
- Create: `src/Iaet.Agents/HumanInteractionBroker.cs`
- Test: `tests/Iaet.Agents.Tests/HumanInteractionBrokerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Agents.Tests/HumanInteractionBrokerTests.cs
using FluentAssertions;
using Iaet.Agents;
using Iaet.Core.Models;

namespace Iaet.Agents.Tests;

public sealed class HumanInteractionBrokerTests
{
    [Fact]
    public async Task RequestAction_writes_to_console_and_waits()
    {
        var input = new StringReader("done\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);

        var request = new HumanActionRequest
        {
            Action = "Please log in",
            Reason = "Auth required",
        };

        await broker.RequestActionAsync(request);

        output.ToString().Should().Contain("Please log in");
        output.ToString().Should().Contain("Auth required");
    }

    [Fact]
    public async Task RequestConfirmation_returns_true_for_y()
    {
        var input = new StringReader("y\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);

        var result = await broker.RequestConfirmationAsync("Proceed?");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestConfirmation_returns_false_for_n()
    {
        var input = new StringReader("n\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);

        var result = await broker.RequestConfirmationAsync("Proceed?");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestConfirmation_returns_true_for_empty_default_yes()
    {
        var input = new StringReader("\n");
        var output = new StringWriter();
        var broker = new HumanInteractionBroker(input, output);

        var result = await broker.RequestConfirmationAsync("Proceed?", defaultYes: true);

        result.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~HumanInteractionBrokerTests" -v quiet`
Expected: Build error — `HumanInteractionBroker` not defined.

- [ ] **Step 3: Implement HumanInteractionBroker**

```csharp
// src/Iaet.Agents/HumanInteractionBroker.cs
using Iaet.Core.Models;

namespace Iaet.Agents;

public sealed class HumanInteractionBroker(TextReader? input = null, TextWriter? output = null)
{
    private readonly TextReader _input = input ?? Console.In;
    private readonly TextWriter _output = output ?? Console.Out;

    public Task RequestActionAsync(HumanActionRequest request, CancellationToken ct = default)
    {
        _output.WriteLine();
        _output.WriteLine($"[Action Required] {request.Action}");
        _output.WriteLine($"  Reason: {request.Reason}");
        if (request.Urgency != "normal")
            _output.WriteLine($"  Urgency: {request.Urgency}");
        _output.Write("  Press Enter when done: ");
        _input.ReadLine();
        return Task.CompletedTask;
    }

    public Task<bool> RequestConfirmationAsync(string prompt, bool defaultYes = false, CancellationToken ct = default)
    {
        var hint = defaultYes ? "[Y/n]" : "[y/N]";
        _output.Write($"{prompt} {hint} ");
        var response = _input.ReadLine()?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(response))
            return Task.FromResult(defaultYes);

        return Task.FromResult(response == "Y" || response == "YES");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~HumanInteractionBrokerTests" -v quiet`
Expected: All 4 tests pass.

- [ ] **Step 5: Register in DI**

Add to `src/Iaet.Agents/ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<HumanInteractionBroker>();
services.AddSingleton<FindingsMerger>();
```

Note: `FindingsMerger` is static-only, so it doesn't need DI registration. Remove it. `HumanInteractionBroker` uses default Console.In/Out when constructed via DI.

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Agents/ tests/Iaet.Agents.Tests/HumanInteractionBrokerTests.cs
git commit -m "feat(agents): implement HumanInteractionBroker for human-in-the-loop"
```

---

## Task 13: RoundExecutor Implementation

**Files:**
- Create: `src/Iaet.Agents/RoundExecutor.cs`
- Test: `tests/Iaet.Agents.Tests/RoundExecutorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Agents.Tests/RoundExecutorTests.cs
using FluentAssertions;
using Iaet.Agents;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using NSubstitute;

namespace Iaet.Agents.Tests;

public sealed class RoundExecutorTests
{
    [Fact]
    public async Task Execute_dispatches_to_matching_agents_in_parallel()
    {
        var agentA = Substitute.For<IInvestigationAgent>();
        agentA.AgentName.Returns("agent-a");
        agentA.ExecuteAsync(Arg.Any<AgentDispatch>(), Arg.Any<ProjectConfig>(), Arg.Any<CancellationToken>())
            .Returns(new AgentFindings { Agent = "agent-a", RoundNumber = 1 });

        var agentB = Substitute.For<IInvestigationAgent>();
        agentB.AgentName.Returns("agent-b");
        agentB.ExecuteAsync(Arg.Any<AgentDispatch>(), Arg.Any<ProjectConfig>(), Arg.Any<CancellationToken>())
            .Returns(new AgentFindings { Agent = "agent-b", RoundNumber = 1 });

        var executor = new RoundExecutor([agentA, agentB]);

        var plan = new RoundPlan
        {
            RoundNumber = 1,
            Rationale = "test",
            Dispatches =
            [
                new AgentDispatch { Agent = "agent-a", Targets = ["url1"] },
                new AgentDispatch { Agent = "agent-b", Targets = ["url2"] },
            ],
        };

        var config = MakeConfig();
        var results = await executor.ExecuteRoundAsync(plan, config);

        results.Should().HaveCount(2);
        results.Select(f => f.Agent).Should().Contain(["agent-a", "agent-b"]);
    }

    [Fact]
    public async Task Execute_skips_dispatches_with_no_matching_agent()
    {
        var agentA = Substitute.For<IInvestigationAgent>();
        agentA.AgentName.Returns("agent-a");
        agentA.ExecuteAsync(Arg.Any<AgentDispatch>(), Arg.Any<ProjectConfig>(), Arg.Any<CancellationToken>())
            .Returns(new AgentFindings { Agent = "agent-a", RoundNumber = 1 });

        var executor = new RoundExecutor([agentA]);

        var plan = new RoundPlan
        {
            RoundNumber = 1,
            Rationale = "test",
            Dispatches =
            [
                new AgentDispatch { Agent = "agent-a" },
                new AgentDispatch { Agent = "nonexistent" },
            ],
        };

        var results = await executor.ExecuteRoundAsync(plan, MakeConfig());

        results.Should().HaveCount(1);
        results[0].Agent.Should().Be("agent-a");
    }

    [Fact]
    public async Task Execute_returns_empty_for_no_dispatches()
    {
        var executor = new RoundExecutor([]);
        var plan = new RoundPlan { RoundNumber = 1, Rationale = "empty" };

        var results = await executor.ExecuteRoundAsync(plan, MakeConfig());

        results.Should().BeEmpty();
    }

    private static ProjectConfig MakeConfig() => new()
    {
        Name = "test",
        DisplayName = "Test",
        TargetType = TargetType.Web,
        EntryPoints = [new EntryPoint { Url = "https://example.com", Label = "Main" }],
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~RoundExecutorTests" -v quiet`
Expected: Build error — `RoundExecutor` not defined.

- [ ] **Step 3: Implement RoundExecutor**

```csharp
// src/Iaet.Agents/RoundExecutor.cs
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Agents;

public sealed class RoundExecutor(IReadOnlyList<IInvestigationAgent> agents)
{
    public async Task<IReadOnlyList<AgentFindings>> ExecuteRoundAsync(
        RoundPlan plan,
        ProjectConfig project,
        CancellationToken ct = default)
    {
        var agentMap = agents.ToDictionary(a => a.AgentName, StringComparer.OrdinalIgnoreCase);

        var tasks = new List<Task<AgentFindings>>();
        foreach (var dispatch in plan.Dispatches)
        {
            if (agentMap.TryGetValue(dispatch.Agent, out var agent))
            {
                tasks.Add(agent.ExecuteAsync(dispatch, project, ct));
            }
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Agents.Tests/ --filter "FullyQualifiedName~RoundExecutorTests" -v quiet`
Expected: All 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Agents/RoundExecutor.cs tests/Iaet.Agents.Tests/RoundExecutorTests.cs
git commit -m "feat(agents): implement RoundExecutor for parallel agent dispatch"
```

---

## Task 14: CLI — ProjectCommand

**Files:**
- Create: `src/Iaet.Cli/Commands/ProjectCommand.cs`
- Modify: `src/Iaet.Cli/Iaet.Cli.csproj` — add project references
- Modify: `src/Iaet.Cli/Program.cs` — register services + command

- [ ] **Step 1: Add project references to CLI**

Add to `src/Iaet.Cli/Iaet.Cli.csproj` in the `<ItemGroup>` with `ProjectReference` entries:

```xml
<ProjectReference Include="..\Iaet.Projects\Iaet.Projects.csproj" />
<ProjectReference Include="..\Iaet.Secrets\Iaet.Secrets.csproj" />
<ProjectReference Include="..\Iaet.Agents\Iaet.Agents.csproj" />
```

- [ ] **Step 2: Create ProjectCommand**

```csharp
// src/Iaet.Cli/Commands/ProjectCommand.cs
// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class ProjectCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("project", "Manage investigation projects");

        cmd.Add(CreateCreateCmd(services));
        cmd.Add(CreateListCmd(services));
        cmd.Add(CreateStatusCmd(services));
        cmd.Add(CreateArchiveCmd(services));
        return cmd;
    }

    private static Command CreateCreateCmd(IServiceProvider services)
    {
        var createCmd = new Command("create", "Create a new investigation project");

        var nameOption = new Option<string>("--name") { Description = "Project name (slug)", Required = true };
        var urlOption = new Option<string>("--url") { Description = "Target starting URL", Required = true };
        var targetTypeOption = new Option<string>("--target-type") { Description = "Target type: web, android, desktop", DefaultValueFactory = _ => "web" };
        var authRequiredOption = new Option<bool>("--auth-required") { Description = "Target requires authentication" };
        var displayNameOption = new Option<string?>("--display-name") { Description = "Human-readable project name" };

        createCmd.Add(nameOption);
        createCmd.Add(urlOption);
        createCmd.Add(targetTypeOption);
        createCmd.Add(authRequiredOption);
        createCmd.Add(displayNameOption);

        createCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);
            var url = parseResult.GetRequiredValue(urlOption);
            var targetTypeStr = parseResult.GetValue(targetTypeOption)!;
            var authRequired = parseResult.GetValue(authRequiredOption);
            var displayName = parseResult.GetValue(displayNameOption) ?? name;

            if (!Enum.TryParse<TargetType>(targetTypeStr, ignoreCase: true, out var targetType))
            {
                Console.WriteLine($"Unknown target type: {targetTypeStr}. Use: web, android, desktop.");
                return;
            }

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();

            var config = new ProjectConfig
            {
                Name = name,
                DisplayName = displayName,
                TargetType = targetType,
                EntryPoints = [new EntryPoint { Url = url, Label = "Main" }],
                AuthRequired = authRequired,
                AuthMethod = authRequired ? "browser-login" : null,
            };

            await store.CreateAsync(config).ConfigureAwait(false);

            GitGuard.EnsureGitignore(Directory.GetCurrentDirectory());

            Console.WriteLine($"Created project: {name}");
            Console.WriteLine($"  Target: {url} ({targetType}, {(authRequired ? "auth-required" : "no-auth")})");
            Console.WriteLine($"  Project dir: {store.GetProjectDirectory(name)}");
        });

        return createCmd;
    }

    private static Command CreateListCmd(IServiceProvider services)
    {
        var listCmd = new Command("list", "List all projects");

        listCmd.SetAction(async (_) =>
        {
            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var projects = await store.ListAsync().ConfigureAwait(false);

            if (projects.Count == 0)
            {
                Console.WriteLine("No projects found.");
                return;
            }

            Console.WriteLine($"{"Name",-25} {"Type",-10} {"Status",-15} {"Rounds",-8} {"Last Activity"}");
            Console.WriteLine(new string('-', 85));
            foreach (var p in projects)
            {
                Console.WriteLine($"{p.Name,-25} {p.TargetType,-10} {p.Status,-15} {p.CurrentRound,-8} {p.LastActivityAt:g}");
            }
        });

        return listCmd;
    }

    private static Command CreateStatusCmd(IServiceProvider services)
    {
        var statusCmd = new Command("status", "Show project status");
        var nameOption = new Option<string>("--name") { Description = "Project name", Required = true };
        statusCmd.Add(nameOption);

        statusCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await store.LoadAsync(name).ConfigureAwait(false);

            if (config is null)
            {
                Console.WriteLine($"Project '{name}' not found.");
                return;
            }

            Console.WriteLine($"Project: {config.DisplayName}");
            Console.WriteLine($"  Name:      {config.Name}");
            Console.WriteLine($"  Type:      {config.TargetType}");
            Console.WriteLine($"  Status:    {config.Status}");
            Console.WriteLine($"  Round:     {config.CurrentRound}");
            Console.WriteLine($"  Auth:      {(config.AuthRequired ? "required" : "none")}");
            Console.WriteLine($"  Created:   {config.CreatedAt:g}");
            Console.WriteLine($"  Last:      {config.LastActivityAt:g}");
            Console.WriteLine($"  Targets:");
            foreach (var ep in config.EntryPoints)
                Console.WriteLine($"    {ep.Label}: {ep.Url}");
        });

        return statusCmd;
    }

    private static Command CreateArchiveCmd(IServiceProvider services)
    {
        var archiveCmd = new Command("archive", "Archive a project");
        var nameOption = new Option<string>("--name") { Description = "Project name", Required = true };
        archiveCmd.Add(nameOption);

        archiveCmd.SetAction(async (parseResult) =>
        {
            var name = parseResult.GetRequiredValue(nameOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();

            await store.ArchiveAsync(name).ConfigureAwait(false);
            Console.WriteLine($"Project '{name}' archived.");
        });

        return archiveCmd;
    }
}
```

- [ ] **Step 3: Create SecretsCommand**

```csharp
// src/Iaet.Cli/Commands/SecretsCommand.cs
// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class SecretsCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("secrets", "Manage project secrets");

        cmd.Add(CreateSetCmd(services));
        cmd.Add(CreateGetCmd(services));
        cmd.Add(CreateListCmd(services));
        return cmd;
    }

    private static Command CreateSetCmd(IServiceProvider services)
    {
        var setCmd = new Command("set", "Set a secret value");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var keyOption = new Option<string>("--key") { Description = "Secret key", Required = true };
        var valueOption = new Option<string>("--value") { Description = "Secret value", Required = true };
        setCmd.Add(projectOption);
        setCmd.Add(keyOption);
        setCmd.Add(valueOption);

        setCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var key = parseResult.GetRequiredValue(keyOption);
            var value = parseResult.GetRequiredValue(valueOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISecretsStore>();
            await store.SetAsync(project, key, value).ConfigureAwait(false);
            Console.WriteLine($"Secret '{key}' set for project '{project}'.");
        });

        return setCmd;
    }

    private static Command CreateGetCmd(IServiceProvider services)
    {
        var getCmd = new Command("get", "Get a secret value");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var keyOption = new Option<string>("--key") { Description = "Secret key", Required = true };
        getCmd.Add(projectOption);
        getCmd.Add(keyOption);

        getCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var key = parseResult.GetRequiredValue(keyOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISecretsStore>();
            var value = await store.GetAsync(project, key).ConfigureAwait(false);

            if (value is null)
                Console.WriteLine($"Secret '{key}' not found in project '{project}'.");
            else
                Console.WriteLine(value);
        });

        return getCmd;
    }

    private static Command CreateListCmd(IServiceProvider services)
    {
        var listCmd = new Command("list", "List secret keys (values hidden)");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        listCmd.Add(projectOption);

        listCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISecretsStore>();
            var secrets = await store.ListAsync(project).ConfigureAwait(false);

            if (secrets.Count == 0)
            {
                Console.WriteLine($"No secrets found for project '{project}'.");
                return;
            }

            Console.WriteLine($"{"Key",-30} {"Value (preview)"}");
            Console.WriteLine(new string('-', 50));
            foreach (var (key, value) in secrets)
            {
                var preview = value.Length <= 4 ? "****" : value[..4] + new string('*', Math.Min(value.Length - 4, 20));
                Console.WriteLine($"{key,-30} {preview}");
            }
        });

        return listCmd;
    }
}
```

- [ ] **Step 4: Update Program.cs**

Add using statements at top of `src/Iaet.Cli/Program.cs`:

```csharp
using Iaet.Agents;
using Iaet.Projects;
using Iaet.Secrets;
```

Add DI registrations inside `ConfigureServices`:

```csharp
var projectsRoot = Path.Combine(Directory.GetCurrentDirectory(), ".iaet-projects");
services.AddIaetProjects(projectsRoot);
services.AddIaetSecrets(projectsRoot);
services.AddIaetAgents(projectsRoot);
```

Add commands to `rootCommand`:

```csharp
ProjectCommand.Create(host.Services),
SecretsCommand.Create(host.Services),
```

- [ ] **Step 5: Verify build**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded.

- [ ] **Step 6: Verify all tests pass**

Run: `dotnet test Iaet.slnx -v quiet`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Cli/ Iaet.slnx
git commit -m "feat(cli): add project and secrets CLI commands"
```

---

## Task 15: Integration — Full Build + Test Verification

**Files:**
- All files from Tasks 1-14

- [ ] **Step 1: Run full solution build**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded with 0 errors.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test Iaet.slnx -v quiet`
Expected: All tests pass (existing + new).

- [ ] **Step 3: Smoke test CLI commands**

Run:
```bash
dotnet run --project src/Iaet.Cli -- project create --name smoke-test --url https://example.com
dotnet run --project src/Iaet.Cli -- project list
dotnet run --project src/Iaet.Cli -- project status --name smoke-test
dotnet run --project src/Iaet.Cli -- secrets set --project smoke-test --key TEST_KEY --value test_value
dotnet run --project src/Iaet.Cli -- secrets list --project smoke-test
dotnet run --project src/Iaet.Cli -- project archive --name smoke-test
dotnet run --project src/Iaet.Cli -- project status --name smoke-test
```

Expected: Each command produces expected output without errors. The project status should show "Archived" after archive.

- [ ] **Step 4: Verify .gitignore was updated**

Check that `.gitignore` contains `.iaet-projects/**/.env.iaet`.

- [ ] **Step 5: Clean up smoke test**

```bash
rm -rf .iaet-projects/smoke-test
```

- [ ] **Step 6: Final commit if any fixups were needed**

```bash
git add -A
git commit -m "fix: integration fixups from smoke testing"
```

Only commit if there were changes. If smoke tests passed cleanly, skip this step.
