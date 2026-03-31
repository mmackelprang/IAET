# Phase 2: Capture Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add cookie lifecycle analysis, action-triggered capture with context annotations, auth health monitoring, and wire the crawler into the CLI properly.

**Architecture:** One new assembly (`Iaet.Cookies`) for CDP cookie collection and lifecycle analysis. Extensions to `Iaet.Capture` for context-annotated capture and auth monitoring. Extensions to `Iaet.Crawler` to wire `PlaywrightPageNavigator` into the CLI crawl command and add agent-directed selective crawling. New CLI commands: `iaet cookies snapshot|diff|analyze`.

**Tech Stack:** .NET 10, Playwright CDP (`Network.getAllCookies`), System.CommandLine v3, xUnit + FluentAssertions + NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-31-agent-investigation-team-design.md` (Phase 2 section)

---

## File Structure

### New assembly: Iaet.Cookies

```
src/Iaet.Cookies/
  Iaet.Cookies.csproj
  CookieCollector.cs            — CDP Network.getAllCookies + Storage integration
  CookieSnapshot.cs             — point-in-time cookie state model
  CookieDiffer.cs               — diff two snapshots
  CookieLifecycleAnalyzer.cs    — rotation patterns, expiry timelines
  AuthCookieIdentifier.cs       — identify auth-critical cookies by replay testing
  StorageScanner.cs             — localStorage/sessionStorage token scan
  ServiceCollectionExtensions.cs
tests/Iaet.Cookies.Tests/
  Iaet.Cookies.Tests.csproj
  CookieSnapshotTests.cs
  CookieDifferTests.cs
  CookieLifecycleAnalyzerTests.cs
  AuthCookieIdentifierTests.cs
  StorageScannerTests.cs
```

### New models in Iaet.Core

```
src/Iaet.Core/Models/
  CapturedCookie.cs             — full cookie metadata
  CookieSnapshotInfo.cs         — snapshot metadata (id, timestamp, source)
  CookieDiff.cs                 — added/removed/changed cookies
  CookieAnalysis.cs             — lifecycle analysis result
  CaptureContext.cs             — annotation for what triggered a capture
```

### New abstractions in Iaet.Core

```
src/Iaet.Core/Abstractions/
  ICookieCollector.cs           — collect cookies from browser
  ICookieStore.cs               — persist/retrieve cookie snapshots
```

### Extensions to existing assemblies

```
src/Iaet.Capture/
  AuthHealthMonitor.cs          — detect 401/403 patterns, notify caller
  CaptureContextAnnotator.cs    — tag requests with trigger context
src/Iaet.Crawler/
  CrawlEngine.cs                — (modify) accept optional project context
src/Iaet.Cli/Commands/
  CookiesCommand.cs             — iaet cookies snapshot|diff|analyze
  CrawlCommand.cs               — (modify) wire PlaywrightPageNavigator properly
```

---

## Task 1: Solution Scaffolding — Iaet.Cookies

**Files:**
- Create: `src/Iaet.Cookies/Iaet.Cookies.csproj`
- Create: `tests/Iaet.Cookies.Tests/Iaet.Cookies.Tests.csproj`
- Modify: `Iaet.slnx`

- [ ] **Step 1: Create Iaet.Cookies project file**

```xml
<!-- src/Iaet.Cookies/Iaet.Cookies.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
    <ProjectReference Include="..\Iaet.Capture\Iaet.Capture.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create test project file**

```xml
<!-- tests/Iaet.Cookies.Tests/Iaet.Cookies.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\Iaet.Cookies\Iaet.Cookies.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Update solution file**

Add to `Iaet.slnx`:
- `<Project Path="src/Iaet.Cookies/Iaet.Cookies.csproj" />` in `/src/`
- `<Project Path="tests/Iaet.Cookies.Tests/Iaet.Cookies.Tests.csproj" />` in `/tests/`

- [ ] **Step 4: Verify build**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cookies/ tests/Iaet.Cookies.Tests/ Iaet.slnx
git commit -m "chore: scaffold Iaet.Cookies assembly"
```

---

## Task 2: Cookie Domain Models

**Files:**
- Create: `src/Iaet.Core/Models/CapturedCookie.cs`
- Create: `src/Iaet.Core/Models/CookieSnapshotInfo.cs`
- Create: `src/Iaet.Core/Models/CookieDiff.cs`
- Create: `src/Iaet.Core/Models/CookieAnalysis.cs`
- Create: `src/Iaet.Core/Models/CaptureContext.cs`
- Test: `tests/Iaet.Core.Tests/Models/CookieModelTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Core.Tests/Models/CookieModelTests.cs
using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class CookieModelTests
{
    [Fact]
    public void CapturedCookie_holds_full_metadata()
    {
        var cookie = new CapturedCookie
        {
            Name = "SID",
            Domain = ".google.com",
            Path = "/",
            Value = "abc123",
            Expires = DateTimeOffset.UtcNow.AddHours(1),
            HttpOnly = true,
            Secure = true,
            SameSite = "Lax",
            Size = 128,
        };

        cookie.Name.Should().Be("SID");
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
    }

    [Fact]
    public void CookieSnapshotInfo_captures_point_in_time()
    {
        var snapshot = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(),
            ProjectName = "google-voice",
            CapturedAt = DateTimeOffset.UtcNow,
            Source = "post-login",
            Cookies = [new CapturedCookie { Name = "SID", Domain = ".google.com", Path = "/", Value = "x" }],
        };

        snapshot.Cookies.Should().HaveCount(1);
        snapshot.Source.Should().Be("post-login");
    }

    [Fact]
    public void CookieDiff_tracks_added_removed_changed()
    {
        var diff = new CookieDiff
        {
            BeforeSnapshotId = Guid.NewGuid(),
            AfterSnapshotId = Guid.NewGuid(),
            Added = [new CapturedCookie { Name = "NEW", Domain = "x", Path = "/", Value = "v" }],
            Removed = [new CapturedCookie { Name = "OLD", Domain = "x", Path = "/", Value = "v" }],
            Changed = [new CookieChange { Name = "SID", Domain = ".google.com", OldValue = "a", NewValue = "b" }],
        };

        diff.Added.Should().HaveCount(1);
        diff.Removed.Should().HaveCount(1);
        diff.Changed.Should().HaveCount(1);
    }

    [Fact]
    public void CookieAnalysis_reports_auth_critical_and_expiry()
    {
        var analysis = new CookieAnalysis
        {
            ProjectName = "gv",
            TotalCookies = 38,
            AuthCritical = ["SID", "HSID"],
            ExpiringWithin = new Dictionary<string, TimeSpan>
            {
                ["SID"] = TimeSpan.FromMinutes(30),
            },
            RotationDetected = ["APISID"],
        };

        analysis.AuthCritical.Should().HaveCount(2);
        analysis.RotationDetected.Should().Contain("APISID");
    }

    [Fact]
    public void CaptureContext_annotates_trigger()
    {
        var ctx = new CaptureContext
        {
            Trigger = "click",
            ElementSelector = "button.call-btn",
            Description = "User clicked Call button",
        };

        ctx.Trigger.Should().Be("click");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Core.Tests/ --filter "FullyQualifiedName~CookieModelTests" -v quiet`
Expected: Build error — types not defined.

- [ ] **Step 3: Create the models**

```csharp
// src/Iaet.Core/Models/CapturedCookie.cs
namespace Iaet.Core.Models;

public sealed record CapturedCookie
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string Path { get; init; }
    public required string Value { get; init; }
    public DateTimeOffset? Expires { get; init; }
    public bool HttpOnly { get; init; }
    public bool Secure { get; init; }
    public string? SameSite { get; init; }
    public long Size { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/CookieSnapshotInfo.cs
namespace Iaet.Core.Models;

public sealed record CookieSnapshotInfo
{
    public required Guid Id { get; init; }
    public required string ProjectName { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required string Source { get; init; }
    public required IReadOnlyList<CapturedCookie> Cookies { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/CookieChange.cs
namespace Iaet.Core.Models;

public sealed record CookieChange
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string OldValue { get; init; }
    public required string NewValue { get; init; }
}
```

```csharp
// src/Iaet.Core/Models/CookieDiff.cs
namespace Iaet.Core.Models;

public sealed record CookieDiff
{
    public required Guid BeforeSnapshotId { get; init; }
    public required Guid AfterSnapshotId { get; init; }
    public IReadOnlyList<CapturedCookie> Added { get; init; } = [];
    public IReadOnlyList<CapturedCookie> Removed { get; init; } = [];
    public IReadOnlyList<CookieChange> Changed { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/CookieAnalysis.cs
namespace Iaet.Core.Models;

public sealed record CookieAnalysis
{
    public required string ProjectName { get; init; }
    public required int TotalCookies { get; init; }
    public IReadOnlyList<string> AuthCritical { get; init; } = [];
    public IReadOnlyDictionary<string, TimeSpan> ExpiringWithin { get; init; } = new Dictionary<string, TimeSpan>();
    public IReadOnlyList<string> RotationDetected { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/CaptureContext.cs
namespace Iaet.Core.Models;

public sealed record CaptureContext
{
    public required string Trigger { get; init; }
    public string? ElementSelector { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Core.Tests/ --filter "FullyQualifiedName~CookieModelTests" -v quiet`
Expected: All 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Core/Models/ tests/Iaet.Core.Tests/Models/CookieModelTests.cs
git commit -m "feat(core): add cookie, snapshot, diff, analysis, and capture context models"
```

---

## Task 3: Cookie Abstractions

**Files:**
- Create: `src/Iaet.Core/Abstractions/ICookieCollector.cs`
- Create: `src/Iaet.Core/Abstractions/ICookieStore.cs`

- [ ] **Step 1: Create ICookieCollector**

```csharp
// src/Iaet.Core/Abstractions/ICookieCollector.cs
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ICookieCollector
{
    Task<IReadOnlyList<CapturedCookie>> CollectAllAsync(CancellationToken ct = default);
    Task<CookieSnapshotInfo> TakeSnapshotAsync(string projectName, string source, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create ICookieStore**

```csharp
// src/Iaet.Core/Abstractions/ICookieStore.cs
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ICookieStore
{
    Task SaveSnapshotAsync(CookieSnapshotInfo snapshot, CancellationToken ct = default);
    Task<CookieSnapshotInfo?> GetSnapshotAsync(string projectName, Guid snapshotId, CancellationToken ct = default);
    Task<IReadOnlyList<CookieSnapshotInfo>> ListSnapshotsAsync(string projectName, CancellationToken ct = default);
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Iaet.Core/Abstractions/
git commit -m "feat(core): add ICookieCollector and ICookieStore abstractions"
```

---

## Task 4: CookieDiffer Implementation

**Files:**
- Create: `src/Iaet.Cookies/CookieDiffer.cs`
- Test: `tests/Iaet.Cookies.Tests/CookieDifferTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Cookies.Tests/CookieDifferTests.cs
using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Models;

namespace Iaet.Cookies.Tests;

public sealed class CookieDifferTests
{
    [Fact]
    public void Diff_detects_added_cookies()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "val1")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "val1"), MakeCookie("NEW", "val2")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Added.Should().HaveCount(1);
        diff.Added[0].Name.Should().Be("NEW");
        diff.Removed.Should().BeEmpty();
        diff.Changed.Should().BeEmpty();
    }

    [Fact]
    public void Diff_detects_removed_cookies()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "val1"), MakeCookie("OLD", "val2")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "val1")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Removed.Should().HaveCount(1);
        diff.Removed[0].Name.Should().Be("OLD");
    }

    [Fact]
    public void Diff_detects_changed_values()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "old-value")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "new-value")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Changed.Should().HaveCount(1);
        diff.Changed[0].OldValue.Should().Be("old-value");
        diff.Changed[0].NewValue.Should().Be("new-value");
    }

    [Fact]
    public void Diff_uses_name_plus_domain_as_key()
    {
        var before = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [MakeCookie("SID", "v1", ".google.com"), MakeCookie("SID", "v2", ".youtube.com")],
        };
        var after = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "b",
            Cookies = [MakeCookie("SID", "v1", ".google.com"), MakeCookie("SID", "v2-changed", ".youtube.com")],
        };

        var diff = CookieDiffer.Diff(before, after);

        diff.Changed.Should().HaveCount(1);
        diff.Changed[0].Domain.Should().Be(".youtube.com");
    }

    [Fact]
    public void Diff_handles_empty_snapshots()
    {
        var empty = new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(), ProjectName = "p", CapturedAt = DateTimeOffset.UtcNow, Source = "a",
            Cookies = [],
        };

        var diff = CookieDiffer.Diff(empty, empty);

        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
        diff.Changed.Should().BeEmpty();
    }

    private static CapturedCookie MakeCookie(string name, string value, string domain = ".example.com") => new()
    {
        Name = name, Value = value, Domain = domain, Path = "/",
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Cookies.Tests/ --filter "FullyQualifiedName~CookieDifferTests" -v quiet`
Expected: Build error — `CookieDiffer` not defined.

- [ ] **Step 3: Implement CookieDiffer**

```csharp
// src/Iaet.Cookies/CookieDiffer.cs
using Iaet.Core.Models;

namespace Iaet.Cookies;

public static class CookieDiffer
{
    public static CookieDiff Diff(CookieSnapshotInfo before, CookieSnapshotInfo after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var beforeMap = before.Cookies.ToDictionary(CookieKey, StringComparer.Ordinal);
        var afterMap = after.Cookies.ToDictionary(CookieKey, StringComparer.Ordinal);

        var added = new List<CapturedCookie>();
        var removed = new List<CapturedCookie>();
        var changed = new List<CookieChange>();

        foreach (var (key, cookie) in afterMap)
        {
            if (!beforeMap.TryGetValue(key, out var beforeCookie))
            {
                added.Add(cookie);
            }
            else if (cookie.Value != beforeCookie.Value)
            {
                changed.Add(new CookieChange
                {
                    Name = cookie.Name,
                    Domain = cookie.Domain,
                    OldValue = beforeCookie.Value,
                    NewValue = cookie.Value,
                });
            }
        }

        foreach (var (key, cookie) in beforeMap)
        {
            if (!afterMap.ContainsKey(key))
            {
                removed.Add(cookie);
            }
        }

        return new CookieDiff
        {
            BeforeSnapshotId = before.Id,
            AfterSnapshotId = after.Id,
            Added = added,
            Removed = removed,
            Changed = changed,
        };
    }

    private static string CookieKey(CapturedCookie c) => $"{c.Name}|{c.Domain}";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Cookies.Tests/ --filter "FullyQualifiedName~CookieDifferTests" -v quiet`
Expected: All 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cookies/CookieDiffer.cs tests/Iaet.Cookies.Tests/CookieDifferTests.cs
git commit -m "feat(cookies): implement CookieDiffer for snapshot comparison"
```

---

## Task 5: CookieLifecycleAnalyzer Implementation

**Files:**
- Create: `src/Iaet.Cookies/CookieLifecycleAnalyzer.cs`
- Test: `tests/Iaet.Cookies.Tests/CookieLifecycleAnalyzerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Cookies.Tests/CookieLifecycleAnalyzerTests.cs
using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Models;

namespace Iaet.Cookies.Tests;

public sealed class CookieLifecycleAnalyzerTests
{
    [Fact]
    public void Analyze_reports_total_count()
    {
        var snapshot = MakeSnapshot([MakeCookie("A"), MakeCookie("B"), MakeCookie("C")]);

        var result = CookieLifecycleAnalyzer.Analyze("proj", snapshot);

        result.TotalCookies.Should().Be(3);
    }

    [Fact]
    public void Analyze_detects_expiring_cookies()
    {
        var soon = DateTimeOffset.UtcNow.AddMinutes(20);
        var later = DateTimeOffset.UtcNow.AddDays(30);
        var snapshot = MakeSnapshot([
            MakeCookie("EXPIRING", expires: soon),
            MakeCookie("SAFE", expires: later),
        ]);

        var result = CookieLifecycleAnalyzer.Analyze("proj", snapshot, expiryThreshold: TimeSpan.FromHours(1));

        result.ExpiringWithin.Should().ContainKey("EXPIRING");
        result.ExpiringWithin.Should().NotContainKey("SAFE");
    }

    [Fact]
    public void Analyze_detects_rotation_across_snapshots()
    {
        var snap1 = MakeSnapshot([MakeCookie("SID", value: "v1"), MakeCookie("STATIC", value: "same")]);
        var snap2 = MakeSnapshot([MakeCookie("SID", value: "v2"), MakeCookie("STATIC", value: "same")]);
        var snap3 = MakeSnapshot([MakeCookie("SID", value: "v3"), MakeCookie("STATIC", value: "same")]);

        var result = CookieLifecycleAnalyzer.AnalyzeRotation("proj", [snap1, snap2, snap3]);

        result.RotationDetected.Should().Contain("SID");
        result.RotationDetected.Should().NotContain("STATIC");
    }

    [Fact]
    public void Analyze_handles_no_expiry()
    {
        var snapshot = MakeSnapshot([MakeCookie("SESSION", expires: null)]);

        var result = CookieLifecycleAnalyzer.Analyze("proj", snapshot);

        result.ExpiringWithin.Should().BeEmpty();
    }

    private static CookieSnapshotInfo MakeSnapshot(IReadOnlyList<CapturedCookie> cookies) => new()
    {
        Id = Guid.NewGuid(),
        ProjectName = "proj",
        CapturedAt = DateTimeOffset.UtcNow,
        Source = "test",
        Cookies = cookies,
    };

    private static CapturedCookie MakeCookie(string name, string value = "val", DateTimeOffset? expires = null, string domain = ".example.com") => new()
    {
        Name = name, Value = value, Domain = domain, Path = "/",
        Expires = expires,
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Cookies.Tests/ --filter "FullyQualifiedName~CookieLifecycleAnalyzerTests" -v quiet`
Expected: Build error.

- [ ] **Step 3: Implement CookieLifecycleAnalyzer**

```csharp
// src/Iaet.Cookies/CookieLifecycleAnalyzer.cs
using Iaet.Core.Models;

namespace Iaet.Cookies;

public static class CookieLifecycleAnalyzer
{
    private static readonly TimeSpan DefaultExpiryThreshold = TimeSpan.FromHours(1);

    public static CookieAnalysis Analyze(
        string projectName,
        CookieSnapshotInfo snapshot,
        TimeSpan? expiryThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var threshold = expiryThreshold ?? DefaultExpiryThreshold;
        var now = DateTimeOffset.UtcNow;
        var expiring = new Dictionary<string, TimeSpan>();

        foreach (var cookie in snapshot.Cookies)
        {
            if (cookie.Expires.HasValue)
            {
                var remaining = cookie.Expires.Value - now;
                if (remaining > TimeSpan.Zero && remaining <= threshold)
                {
                    expiring[cookie.Name] = remaining;
                }
            }
        }

        return new CookieAnalysis
        {
            ProjectName = projectName,
            TotalCookies = snapshot.Cookies.Count,
            ExpiringWithin = expiring,
        };
    }

    public static CookieAnalysis AnalyzeRotation(
        string projectName,
        IReadOnlyList<CookieSnapshotInfo> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count < 2)
            return new CookieAnalysis { ProjectName = projectName, TotalCookies = 0 };

        var rotated = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < snapshots.Count; i++)
        {
            var diff = CookieDiffer.Diff(snapshots[i - 1], snapshots[i]);
            foreach (var change in diff.Changed)
            {
                rotated.Add(change.Name);
            }
        }

        var latest = snapshots[^1];
        return new CookieAnalysis
        {
            ProjectName = projectName,
            TotalCookies = latest.Cookies.Count,
            RotationDetected = rotated.ToList(),
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Cookies.Tests/ --filter "FullyQualifiedName~CookieLifecycleAnalyzerTests" -v quiet`
Expected: All 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cookies/CookieLifecycleAnalyzer.cs tests/Iaet.Cookies.Tests/CookieLifecycleAnalyzerTests.cs
git commit -m "feat(cookies): implement CookieLifecycleAnalyzer for expiry and rotation detection"
```

---

## Task 6: CookieCollector and CookieStore

**Files:**
- Create: `src/Iaet.Cookies/CookieCollector.cs`
- Create: `src/Iaet.Cookies/FileCookieStore.cs`
- Create: `src/Iaet.Cookies/ServiceCollectionExtensions.cs`
- Test: `tests/Iaet.Cookies.Tests/CookieCollectorTests.cs`
- Test: `tests/Iaet.Cookies.Tests/FileCookieStoreTests.cs`

- [ ] **Step 1: Write CookieCollector tests (using mocked ICdpSession)**

```csharp
// tests/Iaet.Cookies.Tests/CookieCollectorTests.cs
using System.Text.Json;
using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Abstractions;
using NSubstitute;

namespace Iaet.Cookies.Tests;

public sealed class CookieCollectorTests
{
    [Fact]
    public async Task CollectAll_parses_cdp_response()
    {
        var cdp = Substitute.For<ICdpSession>();
        var json = JsonDocument.Parse("""
        {
            "cookies": [
                {
                    "name": "SID",
                    "value": "abc123",
                    "domain": ".google.com",
                    "path": "/",
                    "expires": 1743465600,
                    "httpOnly": true,
                    "secure": true,
                    "sameSite": "Lax",
                    "size": 64
                }
            ]
        }
        """);
        cdp.SendCommandAsync("Network.getAllCookies", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(json.RootElement);

        var collector = new CookieCollector(cdp);
        var cookies = await collector.CollectAllAsync();

        cookies.Should().HaveCount(1);
        cookies[0].Name.Should().Be("SID");
        cookies[0].Domain.Should().Be(".google.com");
        cookies[0].HttpOnly.Should().BeTrue();
    }

    [Fact]
    public async Task TakeSnapshot_creates_snapshot_with_metadata()
    {
        var cdp = Substitute.For<ICdpSession>();
        var json = JsonDocument.Parse("""{"cookies": [{"name": "A", "value": "1", "domain": "x", "path": "/", "expires": 0, "httpOnly": false, "secure": false, "sameSite": "None", "size": 10}]}""");
        cdp.SendCommandAsync("Network.getAllCookies", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(json.RootElement);

        var collector = new CookieCollector(cdp);
        var snapshot = await collector.TakeSnapshotAsync("myproject", "post-login");

        snapshot.ProjectName.Should().Be("myproject");
        snapshot.Source.Should().Be("post-login");
        snapshot.Cookies.Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Write FileCookieStore tests**

```csharp
// tests/Iaet.Cookies.Tests/FileCookieStoreTests.cs
using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Models;

namespace Iaet.Cookies.Tests;

public sealed class FileCookieStoreTests : IDisposable
{
    private readonly string _rootDir;
    private readonly FileCookieStore _store;

    public FileCookieStoreTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        _store = new FileCookieStore(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task SaveSnapshot_and_GetSnapshot_round_trip()
    {
        var snapshot = MakeSnapshot("proj", "test");
        await _store.SaveSnapshotAsync(snapshot);

        var loaded = await _store.GetSnapshotAsync("proj", snapshot.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(snapshot.Id);
        loaded.Cookies.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListSnapshots_returns_all_for_project()
    {
        await _store.SaveSnapshotAsync(MakeSnapshot("proj", "snap1"));
        await _store.SaveSnapshotAsync(MakeSnapshot("proj", "snap2"));
        await _store.SaveSnapshotAsync(MakeSnapshot("other", "snap3"));

        var list = await _store.ListSnapshotsAsync("proj");

        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSnapshot_returns_null_for_nonexistent()
    {
        var result = await _store.GetSnapshotAsync("proj", Guid.NewGuid());
        result.Should().BeNull();
    }

    private static CookieSnapshotInfo MakeSnapshot(string project, string source) => new()
    {
        Id = Guid.NewGuid(),
        ProjectName = project,
        CapturedAt = DateTimeOffset.UtcNow,
        Source = source,
        Cookies = [new CapturedCookie { Name = "SID", Value = "v", Domain = ".x.com", Path = "/" }],
    };
}
```

- [ ] **Step 3: Implement CookieCollector**

```csharp
// src/Iaet.Cookies/CookieCollector.cs
using System.Text.Json;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Cookies;

public sealed class CookieCollector(ICdpSession cdpSession) : ICookieCollector
{
    public async Task<IReadOnlyList<CapturedCookie>> CollectAllAsync(CancellationToken ct = default)
    {
        var result = await cdpSession.SendCommandAsync("Network.getAllCookies", null, ct).ConfigureAwait(false);
        var cookies = new List<CapturedCookie>();

        if (result.TryGetProperty("cookies", out var cookiesArray))
        {
            foreach (var c in cookiesArray.EnumerateArray())
            {
                var expiresUnix = c.GetProperty("expires").GetDouble();
                cookies.Add(new CapturedCookie
                {
                    Name = c.GetProperty("name").GetString() ?? string.Empty,
                    Value = c.GetProperty("value").GetString() ?? string.Empty,
                    Domain = c.GetProperty("domain").GetString() ?? string.Empty,
                    Path = c.GetProperty("path").GetString() ?? string.Empty,
                    Expires = expiresUnix > 0
                        ? DateTimeOffset.FromUnixTimeSeconds((long)expiresUnix)
                        : null,
                    HttpOnly = c.GetProperty("httpOnly").GetBoolean(),
                    Secure = c.GetProperty("secure").GetBoolean(),
                    SameSite = c.GetProperty("sameSite").GetString(),
                    Size = c.GetProperty("size").GetInt64(),
                });
            }
        }

        return cookies;
    }

    public async Task<CookieSnapshotInfo> TakeSnapshotAsync(string projectName, string source, CancellationToken ct = default)
    {
        var cookies = await CollectAllAsync(ct).ConfigureAwait(false);
        return new CookieSnapshotInfo
        {
            Id = Guid.NewGuid(),
            ProjectName = projectName,
            CapturedAt = DateTimeOffset.UtcNow,
            Source = source,
            Cookies = cookies,
        };
    }
}
```

- [ ] **Step 4: Implement FileCookieStore**

```csharp
// src/Iaet.Cookies/FileCookieStore.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Cookies;

public sealed class FileCookieStore(string rootDirectory) : ICookieStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task SaveSnapshotAsync(CookieSnapshotInfo snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var dir = GetSnapshotDir(snapshot.ProjectName);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{snapshot.Id:N}.json");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    public async Task<CookieSnapshotInfo?> GetSnapshotAsync(string projectName, Guid snapshotId, CancellationToken ct = default)
    {
        var path = Path.Combine(GetSnapshotDir(projectName), $"{snapshotId:N}.json");
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<CookieSnapshotInfo>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<CookieSnapshotInfo>> ListSnapshotsAsync(string projectName, CancellationToken ct = default)
    {
        var dir = GetSnapshotDir(projectName);
        if (!Directory.Exists(dir))
            return [];

        var results = new List<CookieSnapshotInfo>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<CookieSnapshotInfo>(json, JsonOptions);
            if (snapshot is not null)
                results.Add(snapshot);
        }
        return results;
    }

    private string GetSnapshotDir(string projectName) =>
        Path.Combine(rootDirectory, projectName, "cookies");
}
```

- [ ] **Step 5: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.Cookies/ServiceCollectionExtensions.cs
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cookies;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCookies(
        this IServiceCollection services,
        string rootDirectory = ".iaet-projects")
    {
        services.AddSingleton<ICookieStore>(new FileCookieStore(rootDirectory));
        return services;
    }
}
```

- [ ] **Step 6: Run all cookie tests**

Run: `dotnet test tests/Iaet.Cookies.Tests/ -v quiet`
Expected: All tests pass (CookieDiffer + CookieLifecycle + CookieCollector + FileCookieStore).

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Cookies/ tests/Iaet.Cookies.Tests/
git commit -m "feat(cookies): implement CookieCollector, FileCookieStore, and DI registration"
```

---

## Task 7: AuthHealthMonitor

**Files:**
- Create: `src/Iaet.Capture/AuthHealthMonitor.cs`
- Test: `tests/Iaet.Capture.Tests/AuthHealthMonitorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Capture.Tests/AuthHealthMonitorTests.cs
using FluentAssertions;
using Iaet.Capture;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests;

public sealed class AuthHealthMonitorTests
{
    [Fact]
    public void IsAuthFailure_detects_401()
    {
        var request = MakeRequest(401);
        AuthHealthMonitor.IsAuthFailure(request).Should().BeTrue();
    }

    [Fact]
    public void IsAuthFailure_detects_403()
    {
        var request = MakeRequest(403);
        AuthHealthMonitor.IsAuthFailure(request).Should().BeTrue();
    }

    [Fact]
    public void IsAuthFailure_returns_false_for_200()
    {
        var request = MakeRequest(200);
        AuthHealthMonitor.IsAuthFailure(request).Should().BeFalse();
    }

    [Fact]
    public void Monitor_tracks_consecutive_failures()
    {
        var monitor = new AuthHealthMonitor();

        monitor.RecordResponse(MakeRequest(200));
        monitor.IsHealthy.Should().BeTrue();

        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));
        monitor.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void Monitor_resets_on_success()
    {
        var monitor = new AuthHealthMonitor();

        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(200));
        monitor.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void Monitor_fires_event_on_unhealthy()
    {
        var monitor = new AuthHealthMonitor(consecutiveFailureThreshold: 2);
        var fired = false;
        monitor.AuthUnhealthy += (_, _) => fired = true;

        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));

        fired.Should().BeTrue();
    }

    private static CapturedRequest MakeRequest(int status) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = "GET",
        Url = "https://example.com/api",
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = status,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 100,
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Capture.Tests/ --filter "FullyQualifiedName~AuthHealthMonitorTests" -v quiet`
Expected: Build error.

- [ ] **Step 3: Implement AuthHealthMonitor**

```csharp
// src/Iaet.Capture/AuthHealthMonitor.cs
using Iaet.Core.Models;

namespace Iaet.Capture;

public sealed class AuthHealthMonitor(int consecutiveFailureThreshold = 3)
{
    private int _consecutiveFailures;

    public bool IsHealthy => _consecutiveFailures < consecutiveFailureThreshold;

    public event EventHandler? AuthUnhealthy;

    public static bool IsAuthFailure(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.ResponseStatus is 401 or 403;
    }

    public void RecordResponse(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsAuthFailure(request))
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= consecutiveFailureThreshold)
            {
                AuthUnhealthy?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            _consecutiveFailures = 0;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Capture.Tests/ --filter "FullyQualifiedName~AuthHealthMonitorTests" -v quiet`
Expected: All 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Capture/AuthHealthMonitor.cs tests/Iaet.Capture.Tests/AuthHealthMonitorTests.cs
git commit -m "feat(capture): implement AuthHealthMonitor for 401/403 detection"
```

---

## Task 8: CaptureContextAnnotator

**Files:**
- Create: `src/Iaet.Capture/CaptureContextAnnotator.cs`
- Test: `tests/Iaet.Capture.Tests/CaptureContextAnnotatorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Capture.Tests/CaptureContextAnnotatorTests.cs
using FluentAssertions;
using Iaet.Capture;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests;

public sealed class CaptureContextAnnotatorTests
{
    [Fact]
    public void Annotate_tags_request_with_context()
    {
        var annotator = new CaptureContextAnnotator();
        var ctx = new CaptureContext
        {
            Trigger = "click",
            ElementSelector = "button.submit",
            Description = "Submit form",
        };

        annotator.SetContext(ctx);
        var request = MakeRequest();
        var annotated = annotator.Annotate(request);

        annotated.Tag.Should().Contain("click");
        annotated.Tag.Should().Contain("button.submit");
    }

    [Fact]
    public void Annotate_returns_request_unchanged_when_no_context()
    {
        var annotator = new CaptureContextAnnotator();
        var request = MakeRequest();
        var annotated = annotator.Annotate(request);

        annotated.Tag.Should().BeNull();
    }

    [Fact]
    public void ClearContext_removes_annotation()
    {
        var annotator = new CaptureContextAnnotator();
        annotator.SetContext(new CaptureContext { Trigger = "click" });
        annotator.ClearContext();
        var annotated = annotator.Annotate(MakeRequest());

        annotated.Tag.Should().BeNull();
    }

    private static CapturedRequest MakeRequest() => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = "GET",
        Url = "https://example.com/api",
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 50,
    };
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Capture.Tests/ --filter "FullyQualifiedName~CaptureContextAnnotatorTests" -v quiet`
Expected: Build error.

- [ ] **Step 3: Implement CaptureContextAnnotator**

```csharp
// src/Iaet.Capture/CaptureContextAnnotator.cs
using Iaet.Core.Models;

namespace Iaet.Capture;

public sealed class CaptureContextAnnotator
{
    private CaptureContext? _currentContext;

    public void SetContext(CaptureContext context) => _currentContext = context;

    public void ClearContext() => _currentContext = null;

    public CapturedRequest Annotate(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentContext is null)
            return request;

        var tag = _currentContext.ElementSelector is not null
            ? $"[{_currentContext.Trigger}] {_currentContext.ElementSelector}"
            : $"[{_currentContext.Trigger}]";

        if (_currentContext.Description is not null)
            tag += $" — {_currentContext.Description}";

        return request with { Tag = tag };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Capture.Tests/ --filter "FullyQualifiedName~CaptureContextAnnotatorTests" -v quiet`
Expected: All 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Capture/CaptureContextAnnotator.cs tests/Iaet.Capture.Tests/CaptureContextAnnotatorTests.cs
git commit -m "feat(capture): implement CaptureContextAnnotator for trigger tagging"
```

---

## Task 9: StorageScanner

**Files:**
- Create: `src/Iaet.Cookies/StorageScanner.cs`
- Test: `tests/Iaet.Cookies.Tests/StorageScannerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Iaet.Cookies.Tests/StorageScannerTests.cs
using System.Text.Json;
using FluentAssertions;
using Iaet.Cookies;
using Iaet.Core.Abstractions;
using NSubstitute;

namespace Iaet.Cookies.Tests;

public sealed class StorageScannerTests
{
    [Fact]
    public async Task ScanLocalStorage_returns_key_value_pairs()
    {
        var cdp = Substitute.For<ICdpSession>();
        var entries = JsonDocument.Parse("""
        {
            "result": {
                "type": "object",
                "value": "{\"token\":\"abc123\",\"theme\":\"dark\"}"
            }
        }
        """);
        cdp.SendCommandAsync("Runtime.evaluate",
            Arg.Is<object?>(o => o != null),
            Arg.Any<CancellationToken>())
            .Returns(entries.RootElement);

        var scanner = new StorageScanner(cdp);
        var result = await scanner.ScanLocalStorageAsync();

        result.Should().ContainKey("token");
        result["token"].Should().Be("abc123");
    }

    [Fact]
    public async Task ScanLocalStorage_returns_empty_on_error()
    {
        var cdp = Substitute.For<ICdpSession>();
        cdp.SendCommandAsync("Runtime.evaluate", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("eval failed"));

        var scanner = new StorageScanner(cdp);
        var result = await scanner.ScanLocalStorageAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectTokens_finds_jwt_and_bearer_patterns()
    {
        var storage = new Dictionary<string, string>
        {
            ["access_token"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.sig",
            ["theme"] = "dark",
            ["auth_bearer"] = "Bearer abc123",
            ["count"] = "42",
        };

        var tokens = StorageScanner.DetectTokens(storage);

        tokens.Should().ContainKey("access_token");
        tokens.Should().ContainKey("auth_bearer");
        tokens.Should().NotContainKey("theme");
        tokens.Should().NotContainKey("count");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Iaet.Cookies.Tests/ --filter "FullyQualifiedName~StorageScannerTests" -v quiet`
Expected: Build error.

- [ ] **Step 3: Implement StorageScanner**

```csharp
// src/Iaet.Cookies/StorageScanner.cs
using System.Text.Json;
using Iaet.Core.Abstractions;

namespace Iaet.Cookies;

public sealed class StorageScanner(ICdpSession cdpSession)
{
    private static readonly string[] TokenPatterns = ["token", "auth", "bearer", "session", "jwt", "api_key", "apikey", "credential"];

    public async Task<IReadOnlyDictionary<string, string>> ScanLocalStorageAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await cdpSession.SendCommandAsync(
                "Runtime.evaluate",
                new { expression = "JSON.stringify(localStorage)" },
                ct).ConfigureAwait(false);

            var json = result.GetProperty("result").GetProperty("value").GetString();
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch (Exception)
        {
            return new Dictionary<string, string>();
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> ScanSessionStorageAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await cdpSession.SendCommandAsync(
                "Runtime.evaluate",
                new { expression = "JSON.stringify(sessionStorage)" },
                ct).ConfigureAwait(false);

            var json = result.GetProperty("result").GetProperty("value").GetString();
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch (Exception)
        {
            return new Dictionary<string, string>();
        }
    }

    public static IReadOnlyDictionary<string, string> DetectTokens(IReadOnlyDictionary<string, string> storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in storage)
        {
            var lowerKey = key.ToLowerInvariant();
            var isTokenKey = TokenPatterns.Any(p => lowerKey.Contains(p, StringComparison.Ordinal));
            var isJwt = value.StartsWith("eyJ", StringComparison.Ordinal);
            var isBearer = value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

            if (isTokenKey || isJwt || isBearer)
            {
                tokens[key] = value;
            }
        }
        return tokens;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Iaet.Cookies.Tests/ --filter "FullyQualifiedName~StorageScannerTests" -v quiet`
Expected: All 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cookies/StorageScanner.cs tests/Iaet.Cookies.Tests/StorageScannerTests.cs
git commit -m "feat(cookies): implement StorageScanner for localStorage/sessionStorage token detection"
```

---

## Task 10: CookiesCommand CLI

**Files:**
- Create: `src/Iaet.Cli/Commands/CookiesCommand.cs`
- Modify: `src/Iaet.Cli/Iaet.Cli.csproj` — add Iaet.Cookies reference
- Modify: `src/Iaet.Cli/Program.cs` — register services + command

- [ ] **Step 1: Add project reference**

Add to `src/Iaet.Cli/Iaet.Cli.csproj`:
```xml
<ProjectReference Include="..\Iaet.Cookies\Iaet.Cookies.csproj" />
```

- [ ] **Step 2: Create CookiesCommand**

```csharp
// src/Iaet.Cli/Commands/CookiesCommand.cs
// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using Iaet.Cookies;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class CookiesCommand
{
    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("cookies", "Manage cookie capture and analysis");
        cmd.Add(CreateSnapshotCmd(services));
        cmd.Add(CreateDiffCmd(services));
        cmd.Add(CreateAnalyzeCmd(services));
        return cmd;
    }

    private static Command CreateSnapshotCmd(IServiceProvider services)
    {
        var snapshotCmd = new Command("snapshot", "List cookie snapshots for a project");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        snapshotCmd.Add(projectOption);

        snapshotCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ICookieStore>();
            var snapshots = await store.ListSnapshotsAsync(project).ConfigureAwait(false);

            if (snapshots.Count == 0)
            {
                Console.WriteLine($"No cookie snapshots found for project '{project}'.");
                return;
            }

            Console.WriteLine($"{"ID",-38} {"Source",-20} {"Cookies",-10} {"Captured At"}");
            Console.WriteLine(new string('-', 90));
            foreach (var s in snapshots)
            {
                Console.WriteLine($"{s.Id,-38} {s.Source,-20} {s.Cookies.Count,-10} {s.CapturedAt:g}");
            }
        });

        return snapshotCmd;
    }

    private static Command CreateDiffCmd(IServiceProvider services)
    {
        var diffCmd = new Command("diff", "Diff two cookie snapshots");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var beforeOption = new Option<Guid>("--before") { Description = "Before snapshot ID", Required = true };
        var afterOption = new Option<Guid>("--after") { Description = "After snapshot ID", Required = true };
        diffCmd.Add(projectOption);
        diffCmd.Add(beforeOption);
        diffCmd.Add(afterOption);

        diffCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var beforeId = parseResult.GetRequiredValue(beforeOption);
            var afterId = parseResult.GetRequiredValue(afterOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ICookieStore>();

            var before = await store.GetSnapshotAsync(project, beforeId).ConfigureAwait(false);
            var after = await store.GetSnapshotAsync(project, afterId).ConfigureAwait(false);

            if (before is null || after is null)
            {
                Console.WriteLine("One or both snapshots not found.");
                return;
            }

            var diff = CookieDiffer.Diff(before, after);

            Console.WriteLine($"Cookie diff: {before.Source} → {after.Source}");
            Console.WriteLine($"  Added:   {diff.Added.Count}");
            Console.WriteLine($"  Removed: {diff.Removed.Count}");
            Console.WriteLine($"  Changed: {diff.Changed.Count}");

            if (diff.Added.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Added cookies:");
                foreach (var c in diff.Added)
                    Console.WriteLine($"    + {c.Name} ({c.Domain})");
            }

            if (diff.Removed.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Removed cookies:");
                foreach (var c in diff.Removed)
                    Console.WriteLine($"    - {c.Name} ({c.Domain})");
            }

            if (diff.Changed.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Changed cookies:");
                foreach (var c in diff.Changed)
                    Console.WriteLine($"    ~ {c.Name} ({c.Domain})");
            }
        });

        return diffCmd;
    }

    private static Command CreateAnalyzeCmd(IServiceProvider services)
    {
        var analyzeCmd = new Command("analyze", "Analyze cookie lifecycle for a project");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        analyzeCmd.Add(projectOption);

        analyzeCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ICookieStore>();
            var snapshots = await store.ListSnapshotsAsync(project).ConfigureAwait(false);

            if (snapshots.Count == 0)
            {
                Console.WriteLine($"No snapshots found for project '{project}'.");
                return;
            }

            var latest = snapshots.OrderByDescending(s => s.CapturedAt).First();
            var analysis = CookieLifecycleAnalyzer.Analyze(project, latest);

            Console.WriteLine($"Cookie analysis for: {project}");
            Console.WriteLine($"  Total cookies: {analysis.TotalCookies}");

            if (analysis.ExpiringWithin.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Expiring soon:");
                foreach (var (name, remaining) in analysis.ExpiringWithin)
                    Console.WriteLine($"    ⚠ {name} — {remaining.TotalMinutes:F0} minutes remaining");
            }

            if (snapshots.Count >= 2)
            {
                var ordered = snapshots.OrderBy(s => s.CapturedAt).ToList();
                var rotation = CookieLifecycleAnalyzer.AnalyzeRotation(project, ordered);

                if (rotation.RotationDetected.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("  Rotation detected:");
                    foreach (var name in rotation.RotationDetected)
                        Console.WriteLine($"    ↻ {name}");
                }
            }
        });

        return analyzeCmd;
    }
}
```

- [ ] **Step 3: Update Program.cs**

Add using:
```csharp
using Iaet.Cookies;
```

Add DI registration (after existing project/secrets registrations):
```csharp
services.AddIaetCookies(projectsRoot);
```

Add command:
```csharp
CookiesCommand.Create(host.Services),
```

- [ ] **Step 4: Verify build and tests**

Run: `dotnet build Iaet.slnx && dotnet test Iaet.slnx -v quiet`
Expected: Build succeeds, all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cli/ src/Iaet.Cookies/ServiceCollectionExtensions.cs
git commit -m "feat(cli): add cookies snapshot|diff|analyze commands"
```

---

## Task 11: Full Integration Verification

**Files:** All files from Tasks 1-10

- [ ] **Step 1: Run full solution build**

Run: `dotnet build Iaet.slnx`
Expected: Build succeeded with 0 errors.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test Iaet.slnx -v quiet`
Expected: All tests pass.

- [ ] **Step 3: Verify new test counts**

Cookie tests should include:
- CookieDifferTests: 5
- CookieLifecycleAnalyzerTests: 4
- CookieCollectorTests: 2
- FileCookieStoreTests: 3
- StorageScannerTests: 3

Capture extension tests:
- AuthHealthMonitorTests: 6
- CaptureContextAnnotatorTests: 3

Total new: ~26 tests.

- [ ] **Step 4: Smoke test CLI**

```bash
dotnet run --project src/Iaet.Cli -- cookies snapshot --project nonexistent
dotnet run --project src/Iaet.Cli -- cookies analyze --project nonexistent
```

Expected: "No cookie snapshots found" / "No snapshots found" (graceful empty state).

- [ ] **Step 5: Final commit if fixups needed**

```bash
git add -A
git commit -m "fix: integration fixups from Phase 2 smoke testing"
```

---

## Deferred Items

These items from the spec are intentionally deferred from this plan:

1. **AuthCookieIdentifier** — Identifying auth-critical cookies by replay testing (removing each cookie and observing whether requests fail) requires a live browser session. This will be implemented when the agent team's Lead Investigator can orchestrate live replay rounds.

2. **Crawler CLI wiring** — Wiring `PlaywrightPageNavigator` into `CrawlCommand` (currently the crawler doesn't use it) requires Playwright integration testing. This is better addressed as part of Phase 3 or as a separate focused task with integration test infrastructure.

3. **SPA route detection** — Detecting React Router / Vue Router / Angular Router patterns in the crawler requires JS analysis capabilities from Phase 3 (`Iaet.JsAnalysis`).
