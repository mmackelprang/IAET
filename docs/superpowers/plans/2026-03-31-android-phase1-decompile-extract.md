# Android Phase 1: Decompilation + Static Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Android APK decompilation and static extraction of API endpoints, auth patterns, manifest permissions, and network security config to IAET.

**Architecture:** New assembly `Iaet.Android` with `JadxRunner` and `ApktoolRunner` for decompilation, plus 4 static extractors that scan decompiled Java/Kotlin source. Reuses `ExtractedUrl` model from `Iaet.JsAnalysis`. New CLI commands (`iaet apk decompile|analyze`). New agent prompt (`agents/apk-analyzer.md`). Android projects use `targetType: "android"` in `project.json`.

**Tech Stack:** .NET 10, System.Diagnostics.Process (shell out to jadx/apktool), regex-based source scanning, System.CommandLine v3, xUnit + FluentAssertions + NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-31-android-apk-analysis-design.md` (Phase 1 section)

---

## File Structure

### New assembly: Iaet.Android

```
src/Iaet.Android/
  Iaet.Android.csproj
  Decompilation/
    JadxRunner.cs                    — shell out to jadx CLI
    ApktoolRunner.cs                 — shell out to apktool CLI
    DecompileResult.cs               — decompilation metadata
  Extractors/
    ApkUrlExtractor.cs               — API endpoint URLs from Java source
    ApkAuthExtractor.cs              — API keys, auth patterns
    ManifestAnalyzer.cs              — AndroidManifest.xml parser
    NetworkSecurityAnalyzer.cs       — network_security_config.xml parser
  ApkAnalysisResult.cs               — aggregated result
  ServiceCollectionExtensions.cs
tests/Iaet.Android.Tests/
  Iaet.Android.Tests.csproj
  Decompilation/
    JadxRunnerTests.cs
    ApktoolRunnerTests.cs
  Extractors/
    ApkUrlExtractorTests.cs
    ApkAuthExtractorTests.cs
    ManifestAnalyzerTests.cs
    NetworkSecurityAnalyzerTests.cs
```

### New models in Iaet.Core

```
src/Iaet.Core/Models/
  ApkInfo.cs                         — package name, version, permissions
  NetworkSecurityConfig.cs           — pinned domains, cleartext policy
```

### New/modified CLI files

```
src/Iaet.Cli/Commands/
  ApkCommand.cs                      — iaet apk decompile|analyze
agents/
  apk-analyzer.md                    — agent prompt
```

---

## Task 1: Solution Scaffolding

**Files:**
- Create: `src/Iaet.Android/Iaet.Android.csproj`
- Create: `tests/Iaet.Android.Tests/Iaet.Android.Tests.csproj`
- Modify: `Iaet.slnx`

- [ ] **Step 1: Create project files**

```xml
<!-- src/Iaet.Android/Iaet.Android.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Iaet.Core\Iaet.Core.csproj" />
    <ProjectReference Include="..\Iaet.JsAnalysis\Iaet.JsAnalysis.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/Iaet.Android.Tests/Iaet.Android.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\Iaet.Android\Iaet.Android.csproj" />
    <ProjectReference Include="..\..\src\Iaet.Core\Iaet.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create README.md** (required for NuGet packaging)

```markdown
<!-- src/Iaet.Android/README.md -->
# Iaet.Android

Android APK decompilation and static analysis for IAET investigation projects.
```

- [ ] **Step 3: Update Iaet.slnx**

Add `src/Iaet.Android/Iaet.Android.csproj` to `/src/` and `tests/Iaet.Android.Tests/Iaet.Android.Tests.csproj` to `/tests/`.

- [ ] **Step 4: Verify build and commit**

Run: `dotnet build Iaet.slnx`

```bash
git add src/Iaet.Android/ tests/Iaet.Android.Tests/ Iaet.slnx
git commit -m "chore: scaffold Iaet.Android assembly"
```

---

## Task 2: Core Models — ApkInfo and NetworkSecurityConfig

**Files:**
- Create: `src/Iaet.Core/Models/ApkInfo.cs`
- Create: `src/Iaet.Core/Models/NetworkSecurityConfig.cs`
- Test: `tests/Iaet.Core.Tests/Models/ApkModelTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Core.Tests/Models/ApkModelTests.cs
using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class ApkModelTests
{
    [Fact]
    public void ApkInfo_holds_package_metadata()
    {
        var info = new ApkInfo
        {
            PackageName = "com.google.android.apps.voice",
            VersionName = "5.20.1",
            VersionCode = 520100,
            MinSdk = 24,
            TargetSdk = 34,
            Permissions = ["android.permission.INTERNET", "android.permission.BLUETOOTH"],
            ExportedServices = ["com.google.voice.SipService"],
        };

        info.PackageName.Should().Be("com.google.android.apps.voice");
        info.Permissions.Should().HaveCount(2);
        info.ExportedServices.Should().HaveCount(1);
    }

    [Fact]
    public void NetworkSecurityConfig_holds_pinning_and_cleartext()
    {
        var config = new NetworkSecurityConfig
        {
            PinnedDomains = [new PinnedDomain { Domain = "api.example.com", Pins = ["sha256/abc123"] }],
            CleartextPermittedDomains = ["debug.example.com"],
            CleartextDefaultPermitted = false,
        };

        config.PinnedDomains.Should().HaveCount(1);
        config.CleartextPermittedDomains.Should().Contain("debug.example.com");
        config.CleartextDefaultPermitted.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Create models**

```csharp
// src/Iaet.Core/Models/ApkInfo.cs
namespace Iaet.Core.Models;

public sealed record ApkInfo
{
    public required string PackageName { get; init; }
    public string? VersionName { get; init; }
    public int? VersionCode { get; init; }
    public int? MinSdk { get; init; }
    public int? TargetSdk { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public IReadOnlyList<string> ExportedServices { get; init; } = [];
    public IReadOnlyList<string> ExportedReceivers { get; init; } = [];
    public IReadOnlyList<string> ExportedProviders { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/PinnedDomain.cs
namespace Iaet.Core.Models;

public sealed record PinnedDomain
{
    public required string Domain { get; init; }
    public IReadOnlyList<string> Pins { get; init; } = [];
}
```

```csharp
// src/Iaet.Core/Models/NetworkSecurityConfig.cs
namespace Iaet.Core.Models;

public sealed record NetworkSecurityConfig
{
    public IReadOnlyList<PinnedDomain> PinnedDomains { get; init; } = [];
    public IReadOnlyList<string> CleartextPermittedDomains { get; init; } = [];
    public bool CleartextDefaultPermitted { get; init; } = true;
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Core.Tests/ --filter "FullyQualifiedName~ApkModelTests" -v quiet`

```bash
git add src/Iaet.Core/Models/ tests/Iaet.Core.Tests/Models/ApkModelTests.cs
git commit -m "feat(core): add ApkInfo and NetworkSecurityConfig models"
```

---

## Task 3: JadxRunner and ApktoolRunner

**Files:**
- Create: `src/Iaet.Android/Decompilation/JadxRunner.cs`
- Create: `src/Iaet.Android/Decompilation/ApktoolRunner.cs`
- Create: `src/Iaet.Android/Decompilation/DecompileResult.cs`
- Test: `tests/Iaet.Android.Tests/Decompilation/JadxRunnerTests.cs`
- Test: `tests/Iaet.Android.Tests/Decompilation/ApktoolRunnerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Android.Tests/Decompilation/JadxRunnerTests.cs
using FluentAssertions;
using Iaet.Android.Decompilation;

namespace Iaet.Android.Tests.Decompilation;

public sealed class JadxRunnerTests
{
    [Fact]
    public void BuildArguments_produces_correct_command()
    {
        var args = JadxRunner.BuildArguments("/path/to/app.apk", "/output/dir");

        args.Should().Contain("-d");
        args.Should().Contain("/output/dir");
        args.Should().Contain("/path/to/app.apk");
    }

    [Fact]
    public void BuildArguments_includes_no_imports_flag()
    {
        var args = JadxRunner.BuildArguments("/app.apk", "/out");

        args.Should().Contain("--no-imports");
    }

    [Fact]
    public async Task RunAsync_throws_when_jadx_not_found()
    {
        var runner = new JadxRunner("nonexistent-jadx-binary-xyz");

        var act = () => runner.RunAsync("/fake.apk", "/fake-out");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
```

```csharp
// tests/Iaet.Android.Tests/Decompilation/ApktoolRunnerTests.cs
using FluentAssertions;
using Iaet.Android.Decompilation;

namespace Iaet.Android.Tests.Decompilation;

public sealed class ApktoolRunnerTests
{
    [Fact]
    public void BuildArguments_produces_decode_command()
    {
        var args = ApktoolRunner.BuildArguments("/path/to/app.apk", "/output/dir");

        args.Should().Contain("d");
        args.Should().Contain("-o");
        args.Should().Contain("/output/dir");
        args.Should().Contain("-f");
        args.Should().Contain("/path/to/app.apk");
    }

    [Fact]
    public async Task RunAsync_throws_when_apktool_not_found()
    {
        var runner = new ApktoolRunner("nonexistent-apktool-binary-xyz");

        var act = () => runner.RunAsync("/fake.apk", "/fake-out");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
```

- [ ] **Step 2: Implement DecompileResult**

```csharp
// src/Iaet.Android/Decompilation/DecompileResult.cs
namespace Iaet.Android.Decompilation;

public sealed record DecompileResult
{
    public required bool Success { get; init; }
    public required string OutputDirectory { get; init; }
    public required string Tool { get; init; }
    public int FileCount { get; init; }
    public long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}
```

- [ ] **Step 3: Implement JadxRunner**

```csharp
// src/Iaet.Android/Decompilation/JadxRunner.cs
using System.Diagnostics;

namespace Iaet.Android.Decompilation;

public sealed class JadxRunner(string jadxPath = "jadx")
{
    public static string BuildArguments(string apkPath, string outputDir)
    {
        return $"-d \"{outputDir}\" --no-imports --no-debug-info \"{apkPath}\"";
    }

    public async Task<DecompileResult> RunAsync(string apkPath, string outputDir, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Verify jadx exists
        try
        {
            using var check = Process.Start(new ProcessStartInfo(jadxPath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (check is null)
                throw new InvalidOperationException($"jadx not found at: {jadxPath}");

            await check.WaitForExitAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // jadx check can fail in many ways
        catch (Exception) when (jadxPath == "nonexistent-jadx-binary-xyz" || jadxPath != "jadx")
        {
            throw new InvalidOperationException($"jadx not found at: {jadxPath}");
        }
#pragma warning restore CA1031

        Directory.CreateDirectory(outputDir);

        var args = BuildArguments(apkPath, outputDir);
        var psi = new ProcessStartInfo(jadxPath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start jadx at: {jadxPath}");

        var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        sw.Stop();

        var fileCount = Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "*.java", SearchOption.AllDirectories).Length
            : 0;

        return new DecompileResult
        {
            Success = proc.ExitCode == 0,
            OutputDirectory = outputDir,
            Tool = "jadx",
            FileCount = fileCount,
            DurationMs = sw.ElapsedMilliseconds,
            ErrorMessage = proc.ExitCode != 0 ? stderr : null,
        };
    }
}
```

- [ ] **Step 4: Implement ApktoolRunner**

```csharp
// src/Iaet.Android/Decompilation/ApktoolRunner.cs
using System.Diagnostics;

namespace Iaet.Android.Decompilation;

public sealed class ApktoolRunner(string apktoolPath = "apktool")
{
    public static string BuildArguments(string apkPath, string outputDir)
    {
        return $"d -o \"{outputDir}\" -f \"{apkPath}\"";
    }

    public async Task<DecompileResult> RunAsync(string apkPath, string outputDir, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var check = Process.Start(new ProcessStartInfo(apktoolPath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (check is null)
                throw new InvalidOperationException($"apktool not found at: {apktoolPath}");

            await check.WaitForExitAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception) when (apktoolPath != "apktool")
        {
            throw new InvalidOperationException($"apktool not found at: {apktoolPath}");
        }
#pragma warning restore CA1031

        Directory.CreateDirectory(outputDir);

        var args = BuildArguments(apkPath, outputDir);
        var psi = new ProcessStartInfo(apktoolPath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start apktool at: {apktoolPath}");

        var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        sw.Stop();

        return new DecompileResult
        {
            Success = proc.ExitCode == 0,
            OutputDirectory = outputDir,
            Tool = "apktool",
            DurationMs = sw.ElapsedMilliseconds,
            ErrorMessage = proc.ExitCode != 0 ? stderr : null,
        };
    }
}
```

- [ ] **Step 5: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Android.Tests/ --filter "FullyQualifiedName~RunnerTests" -v quiet`

```bash
git add src/Iaet.Android/Decompilation/ tests/Iaet.Android.Tests/Decompilation/
git commit -m "feat(android): implement JadxRunner and ApktoolRunner for APK decompilation"
```

---

## Task 4: ApkUrlExtractor

**Files:**
- Create: `src/Iaet.Android/Extractors/ApkUrlExtractor.cs`
- Test: `tests/Iaet.Android.Tests/Extractors/ApkUrlExtractorTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Android.Tests/Extractors/ApkUrlExtractorTests.cs
using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class ApkUrlExtractorTests
{
    [Fact]
    public void Extract_finds_string_literal_urls()
    {
        var java = """
            public class ApiClient {
                private static final String BASE_URL = "https://api.example.com/v2";
                private static final String WS_URL = "wss://ws.example.com/realtime";
            }
            """;

        var urls = ApkUrlExtractor.Extract(java, "ApiClient.java");

        urls.Should().Contain(u => u.Url == "https://api.example.com/v2");
        urls.Should().Contain(u => u.Url == "wss://ws.example.com/realtime");
    }

    [Fact]
    public void Extract_finds_retrofit_annotations()
    {
        var java = """
            public interface VoiceApi {
                @GET("users/{id}")
                Call<User> getUser(@Path("id") String userId);

                @POST("messages")
                Call<Message> sendMessage(@Body MessageRequest request);

                @DELETE("threads/{threadId}")
                Call<Void> deleteThread(@Path("threadId") String threadId);
            }
            """;

        var urls = ApkUrlExtractor.Extract(java, "VoiceApi.java");

        urls.Should().Contain(u => u.Url == "users/{id}" && u.HttpMethod == "GET");
        urls.Should().Contain(u => u.Url == "messages" && u.HttpMethod == "POST");
        urls.Should().Contain(u => u.Url == "threads/{threadId}" && u.HttpMethod == "DELETE");
    }

    [Fact]
    public void Extract_finds_okhttp_urls()
    {
        var java = """
            Request request = new Request.Builder()
                .url("https://clients6.google.com/voice/v1/voiceclient/api")
                .build();
            """;

        var urls = ApkUrlExtractor.Extract(java, "Service.java");

        urls.Should().Contain(u => u.Url == "https://clients6.google.com/voice/v1/voiceclient/api");
    }

    [Fact]
    public void Extract_works_with_obfuscated_code()
    {
        var java = """
            public class a {
                private static final String b = "https://api.secret.com/v1/data";
                public void c() {
                    new Request.Builder().url("https://api.secret.com/v1/users").build();
                }
            }
            """;

        var urls = ApkUrlExtractor.Extract(java, "a.java");

        urls.Should().Contain(u => u.Url == "https://api.secret.com/v1/data");
        urls.Should().Contain(u => u.Url == "https://api.secret.com/v1/users");
    }

    [Fact]
    public void Extract_ignores_non_api_urls()
    {
        var java = """
            String icon = "https://example.com/icon.png";
            String stylesheet = "https://example.com/style.css";
            String api = "https://example.com/api/v1/data";
            """;

        var urls = ApkUrlExtractor.Extract(java, "App.java");

        urls.Should().NotContain(u => u.Url.Contains(".png"));
        urls.Should().NotContain(u => u.Url.Contains(".css"));
        urls.Should().Contain(u => u.Url.Contains("/api/v1/data"));
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        ApkUrlExtractor.Extract("", "empty.java").Should().BeEmpty();
    }

    [Fact]
    public void ExtractFromDirectory_scans_all_java_files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Api.java"),
                """private static final String URL = "https://api.example.com/v1";""");
            File.WriteAllText(Path.Combine(tempDir, "Other.java"),
                """String x = "not a url";""");

            var urls = ApkUrlExtractor.ExtractFromDirectory(tempDir);

            urls.Should().Contain(u => u.Url == "https://api.example.com/v1");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Implement ApkUrlExtractor**

```csharp
// src/Iaet.Android/Extractors/ApkUrlExtractor.cs
using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

public static partial class ApkUrlExtractor
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp",
        ".css", ".woff", ".woff2", ".ttf", ".eot", ".mp3", ".mp4",
    };

    public static IReadOnlyList<ExtractedUrl> Extract(string javaSource, string sourceFile)
    {
        if (string.IsNullOrEmpty(javaSource))
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ExtractedUrl>();
        var lines = javaSource.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            // Retrofit annotations: @GET("path"), @POST("path"), etc.
            foreach (Match match in RetrofitPattern().Matches(line))
            {
                var method = match.Groups[1].Value.ToUpperInvariant();
                var path = match.Groups[2].Value;
                if (seen.Add($"{method}:{path}"))
                {
                    results.Add(new ExtractedUrl
                    {
                        Url = path,
                        HttpMethod = method,
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        Confidence = ConfidenceLevel.High,
                        Context = "Retrofit annotation",
                    });
                }
            }

            // String literal URLs (http/https/wss)
            foreach (Match match in UrlStringPattern().Matches(line))
            {
                var url = match.Groups[1].Value;
                if (IsIgnoredUrl(url)) continue;
                if (!seen.Add(url)) continue;

                results.Add(new ExtractedUrl
                {
                    Url = url,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    Confidence = ConfidenceLevel.High,
                    Context = line.Trim().Length > 120 ? line.Trim()[..120] : line.Trim(),
                });
            }
        }

        return results;
    }

    public static IReadOnlyList<ExtractedUrl> ExtractFromDirectory(string decompiledDir)
    {
        ArgumentNullException.ThrowIfNull(decompiledDir);

        if (!Directory.Exists(decompiledDir))
            return [];

        var allUrls = new List<ExtractedUrl>();

        foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(decompiledDir, file);
            allUrls.AddRange(Extract(source, relativePath));
        }

        // Deduplicate across files (keep first occurrence)
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return allUrls.Where(u => seen.Add(u.Url)).ToList();
    }

    private static bool IsIgnoredUrl(string url)
    {
        foreach (var ext in IgnoredExtensions)
        {
            if (url.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    [GeneratedRegex("""@(GET|POST|PUT|DELETE|PATCH|HEAD)\("([^"]+)"\)""")]
    private static partial Regex RetrofitPattern();

    [GeneratedRegex(""""((?:https?://|wss?://)[^"]+)"""")]
    private static partial Regex UrlStringPattern();
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Android.Tests/ --filter "FullyQualifiedName~ApkUrlExtractorTests" -v quiet`

```bash
git add src/Iaet.Android/Extractors/ApkUrlExtractor.cs tests/Iaet.Android.Tests/Extractors/ApkUrlExtractorTests.cs
git commit -m "feat(android): implement ApkUrlExtractor for API endpoint discovery in decompiled source"
```

---

## Task 5: ApkAuthExtractor

**Files:**
- Create: `src/Iaet.Android/Extractors/ApkAuthExtractor.cs`
- Test: `tests/Iaet.Android.Tests/Extractors/ApkAuthExtractorTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Android.Tests/Extractors/ApkAuthExtractorTests.cs
using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class ApkAuthExtractorTests
{
    [Fact]
    public void Extract_finds_api_key_constants()
    {
        var java = """
            public class Config {
                public static final String API_KEY = "AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg";
                public static final String CLIENT_ID = "1234567890-abcdefg.apps.googleusercontent.com";
            }
            """;

        var auth = ApkAuthExtractor.Extract(java, "Config.java");

        auth.Should().Contain(a => a.Key == "API_KEY" && a.Value.StartsWith("AIza", StringComparison.Ordinal));
        auth.Should().Contain(a => a.Key == "CLIENT_ID");
    }

    [Fact]
    public void Extract_finds_header_construction()
    {
        var java = """
            request.addHeader("Authorization", "Bearer " + token);
            request.addHeader("X-Api-Key", apiKey);
            """;

        var auth = ApkAuthExtractor.Extract(java, "Client.java");

        auth.Should().Contain(a => a.Key == "Authorization");
        auth.Should().Contain(a => a.Key == "X-Api-Key");
    }

    [Fact]
    public void Extract_works_with_obfuscated_constants()
    {
        var java = """
            public class a {
                static final String b = "AIzaSyBGb5fGAyC-pRcRU6MUHb__b_vKha71HRE";
            }
            """;

        var auth = ApkAuthExtractor.Extract(java, "a.java");

        auth.Should().Contain(a => a.Value.StartsWith("AIza", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_handles_empty()
    {
        ApkAuthExtractor.Extract("", "empty.java").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Implement ApkAuthExtractor**

```csharp
// src/Iaet.Android/Extractors/ApkAuthExtractor.cs
using System.Text.RegularExpressions;

namespace Iaet.Android.Extractors;

public static partial class ApkAuthExtractor
{
    public sealed record AuthEntry
    {
        public required string Key { get; init; }
        public required string Value { get; init; }
        public required string SourceFile { get; init; }
        public int? LineNumber { get; init; }
        public required string PatternType { get; init; }
    }

    public static IReadOnlyList<AuthEntry> Extract(string javaSource, string sourceFile)
    {
        if (string.IsNullOrEmpty(javaSource))
            return [];

        var results = new List<AuthEntry>();
        var lines = javaSource.Split('\n');

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            // API key constants: static final String KEY_NAME = "value"
            foreach (Match match in ApiKeyConstantPattern().Matches(line))
            {
                results.Add(new AuthEntry
                {
                    Key = match.Groups[1].Value,
                    Value = match.Groups[2].Value,
                    SourceFile = sourceFile,
                    LineNumber = lineIdx + 1,
                    PatternType = "constant",
                });
            }

            // Google API keys (AIza...) even in obfuscated code
            foreach (Match match in GoogleApiKeyPattern().Matches(line))
            {
                var keyValue = match.Groups[1].Value;
                if (!results.Any(r => r.Value == keyValue))
                {
                    results.Add(new AuthEntry
                    {
                        Key = "GOOGLE_API_KEY",
                        Value = keyValue,
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        PatternType = "google-api-key",
                    });
                }
            }

            // Header construction: addHeader("Name", ...)
            foreach (Match match in HeaderPattern().Matches(line))
            {
                var headerName = match.Groups[1].Value;
                if (headerName.Contains("auth", StringComparison.OrdinalIgnoreCase)
                    || headerName.Contains("api-key", StringComparison.OrdinalIgnoreCase)
                    || headerName.Contains("token", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new AuthEntry
                    {
                        Key = headerName,
                        Value = "[dynamic]",
                        SourceFile = sourceFile,
                        LineNumber = lineIdx + 1,
                        PatternType = "header",
                    });
                }
            }
        }

        return results;
    }

    [GeneratedRegex("""(?:static\s+)?(?:final\s+)?String\s+(\w*(?:KEY|SECRET|TOKEN|CLIENT_ID|API)\w*)\s*=\s*"([^"]+)"""", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyConstantPattern();

    [GeneratedRegex(""""(AIza[A-Za-z0-9_-]{33,39})"""")]
    private static partial Regex GoogleApiKeyPattern();

    [GeneratedRegex("""(?:addHeader|header)\("([^"]+)"""")]
    private static partial Regex HeaderPattern();
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Android.Tests/ --filter "FullyQualifiedName~ApkAuthExtractorTests" -v quiet`

```bash
git add src/Iaet.Android/Extractors/ApkAuthExtractor.cs tests/Iaet.Android.Tests/Extractors/ApkAuthExtractorTests.cs
git commit -m "feat(android): implement ApkAuthExtractor for API key and auth pattern discovery"
```

---

## Task 6: ManifestAnalyzer

**Files:**
- Create: `src/Iaet.Android/Extractors/ManifestAnalyzer.cs`
- Test: `tests/Iaet.Android.Tests/Extractors/ManifestAnalyzerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Android.Tests/Extractors/ManifestAnalyzerTests.cs
using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class ManifestAnalyzerTests
{
    private const string SampleManifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <manifest xmlns:android="http://schemas.android.com/apk/res/android"
            package="com.example.myapp"
            android:versionCode="100"
            android:versionName="1.2.3">
            <uses-sdk android:minSdkVersion="24" android:targetSdkVersion="34" />
            <uses-permission android:name="android.permission.INTERNET" />
            <uses-permission android:name="android.permission.BLUETOOTH" />
            <uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />
            <application>
                <service android:name=".SipService" android:exported="true" />
                <service android:name=".InternalService" android:exported="false" />
                <receiver android:name=".BootReceiver" android:exported="true">
                    <intent-filter>
                        <action android:name="android.intent.action.BOOT_COMPLETED" />
                    </intent-filter>
                </receiver>
            </application>
        </manifest>
        """;

    [Fact]
    public void Parse_extracts_package_info()
    {
        var info = ManifestAnalyzer.Parse(SampleManifest);

        info.PackageName.Should().Be("com.example.myapp");
        info.VersionName.Should().Be("1.2.3");
        info.VersionCode.Should().Be(100);
        info.MinSdk.Should().Be(24);
        info.TargetSdk.Should().Be(34);
    }

    [Fact]
    public void Parse_extracts_permissions()
    {
        var info = ManifestAnalyzer.Parse(SampleManifest);

        info.Permissions.Should().Contain("android.permission.INTERNET");
        info.Permissions.Should().Contain("android.permission.BLUETOOTH");
        info.Permissions.Should().Contain("android.permission.BLUETOOTH_CONNECT");
    }

    [Fact]
    public void Parse_extracts_exported_components()
    {
        var info = ManifestAnalyzer.Parse(SampleManifest);

        info.ExportedServices.Should().Contain(".SipService");
        info.ExportedServices.Should().NotContain(".InternalService");
        info.ExportedReceivers.Should().Contain(".BootReceiver");
    }

    [Fact]
    public void Parse_handles_empty()
    {
        var info = ManifestAnalyzer.Parse("");

        info.PackageName.Should().Be("unknown");
        info.Permissions.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Implement ManifestAnalyzer**

```csharp
// src/Iaet.Android/Extractors/ManifestAnalyzer.cs
using System.Xml.Linq;
using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

public static class ManifestAnalyzer
{
    private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";

    public static ApkInfo Parse(string manifestXml)
    {
        if (string.IsNullOrWhiteSpace(manifestXml))
            return new ApkInfo { PackageName = "unknown" };

        XDocument doc;
        try
        {
            doc = XDocument.Parse(manifestXml);
        }
#pragma warning disable CA1031
        catch (Exception)
        {
            return new ApkInfo { PackageName = "unknown" };
        }
#pragma warning restore CA1031

        var manifest = doc.Root;
        if (manifest is null)
            return new ApkInfo { PackageName = "unknown" };

        var packageName = manifest.Attribute("package")?.Value ?? "unknown";
        var versionName = manifest.Attribute(AndroidNs + "versionName")?.Value;
        _ = int.TryParse(manifest.Attribute(AndroidNs + "versionCode")?.Value, out var versionCode);

        var usesSdk = manifest.Element("uses-sdk");
        _ = int.TryParse(usesSdk?.Attribute(AndroidNs + "minSdkVersion")?.Value, out var minSdk);
        _ = int.TryParse(usesSdk?.Attribute(AndroidNs + "targetSdkVersion")?.Value, out var targetSdk);

        var permissions = manifest.Elements("uses-permission")
            .Select(e => e.Attribute(AndroidNs + "name")?.Value)
            .Where(p => p is not null)
            .Cast<string>()
            .ToList();

        var app = manifest.Element("application");

        var exportedServices = ExtractExportedComponents(app, "service");
        var exportedReceivers = ExtractExportedComponents(app, "receiver");
        var exportedProviders = ExtractExportedComponents(app, "provider");

        return new ApkInfo
        {
            PackageName = packageName,
            VersionName = versionName,
            VersionCode = versionCode > 0 ? versionCode : null,
            MinSdk = minSdk > 0 ? minSdk : null,
            TargetSdk = targetSdk > 0 ? targetSdk : null,
            Permissions = permissions,
            ExportedServices = exportedServices,
            ExportedReceivers = exportedReceivers,
            ExportedProviders = exportedProviders,
        };
    }

    public static ApkInfo ParseFile(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return new ApkInfo { PackageName = "unknown" };

        return Parse(File.ReadAllText(manifestPath));
    }

    private static IReadOnlyList<string> ExtractExportedComponents(XElement? app, string elementName)
    {
        if (app is null)
            return [];

        return app.Elements(elementName)
            .Where(e => e.Attribute(AndroidNs + "exported")?.Value == "true")
            .Select(e => e.Attribute(AndroidNs + "name")?.Value)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();
    }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Android.Tests/ --filter "FullyQualifiedName~ManifestAnalyzerTests" -v quiet`

```bash
git add src/Iaet.Android/Extractors/ManifestAnalyzer.cs tests/Iaet.Android.Tests/Extractors/ManifestAnalyzerTests.cs
git commit -m "feat(android): implement ManifestAnalyzer for AndroidManifest.xml parsing"
```

---

## Task 7: NetworkSecurityAnalyzer

**Files:**
- Create: `src/Iaet.Android/Extractors/NetworkSecurityAnalyzer.cs`
- Test: `tests/Iaet.Android.Tests/Extractors/NetworkSecurityAnalyzerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/Iaet.Android.Tests/Extractors/NetworkSecurityAnalyzerTests.cs
using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class NetworkSecurityAnalyzerTests
{
    [Fact]
    public void Parse_extracts_pinned_domains()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <network-security-config>
                <domain-config>
                    <domain includeSubdomains="true">api.example.com</domain>
                    <pin-set>
                        <pin digest="SHA-256">abc123def456</pin>
                        <pin digest="SHA-256">xyz789ghi012</pin>
                    </pin-set>
                </domain-config>
            </network-security-config>
            """;

        var config = NetworkSecurityAnalyzer.Parse(xml);

        config.PinnedDomains.Should().HaveCount(1);
        config.PinnedDomains[0].Domain.Should().Be("api.example.com");
        config.PinnedDomains[0].Pins.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_extracts_cleartext_policy()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <network-security-config>
                <base-config cleartextTrafficPermitted="false" />
                <domain-config cleartextTrafficPermitted="true">
                    <domain>debug.example.com</domain>
                </domain-config>
            </network-security-config>
            """;

        var config = NetworkSecurityAnalyzer.Parse(xml);

        config.CleartextDefaultPermitted.Should().BeFalse();
        config.CleartextPermittedDomains.Should().Contain("debug.example.com");
    }

    [Fact]
    public void Parse_handles_empty()
    {
        var config = NetworkSecurityAnalyzer.Parse("");

        config.PinnedDomains.Should().BeEmpty();
        config.CleartextDefaultPermitted.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Implement NetworkSecurityAnalyzer**

```csharp
// src/Iaet.Android/Extractors/NetworkSecurityAnalyzer.cs
using System.Xml.Linq;
using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

public static class NetworkSecurityAnalyzer
{
    public static NetworkSecurityConfig Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new NetworkSecurityConfig();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
#pragma warning disable CA1031
        catch (Exception)
        {
            return new NetworkSecurityConfig();
        }
#pragma warning restore CA1031

        var root = doc.Root;
        if (root is null)
            return new NetworkSecurityConfig();

        // Base config cleartext
        var baseConfig = root.Element("base-config");
        var cleartextDefault = true;
        if (baseConfig?.Attribute("cleartextTrafficPermitted")?.Value == "false")
            cleartextDefault = false;

        var pinnedDomains = new List<PinnedDomain>();
        var cleartextDomains = new List<string>();

        foreach (var domainConfig in root.Elements("domain-config"))
        {
            var domains = domainConfig.Elements("domain")
                .Select(d => d.Value.Trim())
                .ToList();

            // Check for pins
            var pinSet = domainConfig.Element("pin-set");
            if (pinSet is not null)
            {
                var pins = pinSet.Elements("pin")
                    .Select(p => p.Value.Trim())
                    .ToList();

                foreach (var domain in domains)
                {
                    pinnedDomains.Add(new PinnedDomain { Domain = domain, Pins = pins });
                }
            }

            // Check for cleartext
            if (domainConfig.Attribute("cleartextTrafficPermitted")?.Value == "true")
            {
                cleartextDomains.AddRange(domains);
            }
        }

        return new NetworkSecurityConfig
        {
            PinnedDomains = pinnedDomains,
            CleartextPermittedDomains = cleartextDomains,
            CleartextDefaultPermitted = cleartextDefault,
        };
    }

    public static NetworkSecurityConfig ParseFile(string path)
    {
        if (!File.Exists(path))
            return new NetworkSecurityConfig();

        return Parse(File.ReadAllText(path));
    }
}
```

- [ ] **Step 3: Run tests, verify pass, commit**

Run: `dotnet test tests/Iaet.Android.Tests/ --filter "FullyQualifiedName~NetworkSecurityAnalyzerTests" -v quiet`

```bash
git add src/Iaet.Android/Extractors/NetworkSecurityAnalyzer.cs tests/Iaet.Android.Tests/Extractors/NetworkSecurityAnalyzerTests.cs
git commit -m "feat(android): implement NetworkSecurityAnalyzer for cert pinning and cleartext config"
```

---

## Task 8: ApkAnalysisResult and ServiceCollectionExtensions

**Files:**
- Create: `src/Iaet.Android/ApkAnalysisResult.cs`
- Create: `src/Iaet.Android/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Create ApkAnalysisResult**

```csharp
// src/Iaet.Android/ApkAnalysisResult.cs
using Iaet.Android.Extractors;
using Iaet.Core.Models;

namespace Iaet.Android;

public sealed record ApkAnalysisResult
{
    public required ApkInfo Manifest { get; init; }
    public required NetworkSecurityConfig NetworkSecurity { get; init; }
    public required IReadOnlyList<ExtractedUrl> Urls { get; init; }
    public required IReadOnlyList<ApkAuthExtractor.AuthEntry> AuthEntries { get; init; }
    public string? DecompiledPath { get; init; }
    public int JavaFileCount { get; init; }
}
```

- [ ] **Step 2: Create ServiceCollectionExtensions**

```csharp
// src/Iaet.Android/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Android;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetAndroid(this IServiceCollection services)
    {
        return services;
    }
}
```

- [ ] **Step 3: Verify build and commit**

Run: `dotnet build Iaet.slnx`

```bash
git add src/Iaet.Android/ApkAnalysisResult.cs src/Iaet.Android/ServiceCollectionExtensions.cs
git commit -m "feat(android): add ApkAnalysisResult model and DI registration"
```

---

## Task 9: CLI Commands + Agent Prompt

**Files:**
- Create: `src/Iaet.Cli/Commands/ApkCommand.cs`
- Create: `agents/apk-analyzer.md`
- Modify: `src/Iaet.Cli/Iaet.Cli.csproj` — add Iaet.Android reference
- Modify: `src/Iaet.Cli/Program.cs` — register services + command

- [ ] **Step 1: Add project reference**

Add to `src/Iaet.Cli/Iaet.Cli.csproj`:
```xml
<ProjectReference Include="..\Iaet.Android\Iaet.Android.csproj" />
```

- [ ] **Step 2: Create ApkCommand**

```csharp
// src/Iaet.Cli/Commands/ApkCommand.cs
// CA1303: CLI output strings are intentionally not localized.
#pragma warning disable CA1303

using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Android.Decompilation;
using Iaet.Android.Extractors;
using Iaet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

internal static class ApkCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    internal static Command Create(IServiceProvider services)
    {
        var cmd = new Command("apk", "Android APK analysis");
        cmd.Add(CreateDecompileCmd(services));
        cmd.Add(CreateAnalyzeCmd(services));
        return cmd;
    }

    private static Command CreateDecompileCmd(IServiceProvider services)
    {
        var decompileCmd = new Command("decompile", "Decompile an APK file");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };
        var apkOption = new Option<FileInfo>("--apk") { Description = "Path to APK file", Required = true };
        var jadxPathOption = new Option<string>("--jadx-path") { Description = "Path to jadx executable", DefaultValueFactory = _ => "jadx" };
        var mappingOption = new Option<FileInfo?>("--mapping") { Description = "ProGuard mapping.txt file" };

        decompileCmd.Add(projectOption);
        decompileCmd.Add(apkOption);
        decompileCmd.Add(jadxPathOption);
        decompileCmd.Add(mappingOption);

        decompileCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);
            var apkFile = parseResult.GetRequiredValue(apkOption);
            var jadxPath = parseResult.GetValue(jadxPathOption)!;
            var mapping = parseResult.GetValue(mappingOption);

            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await projectStore.LoadAsync(project).ConfigureAwait(false);

            if (config is null)
            {
                Console.WriteLine($"Project '{project}' not found.");
                return;
            }

            var projectDir = projectStore.GetProjectDirectory(project);
            var apkDir = Path.Combine(projectDir, "apk");
            Directory.CreateDirectory(apkDir);

            // Copy APK to project
            var destApk = Path.Combine(apkDir, "app.apk");
            File.Copy(apkFile.FullName, destApk, overwrite: true);
            Console.WriteLine($"APK copied to: {destApk}");

            // Copy mapping if provided
            if (mapping is not null)
            {
                File.Copy(mapping.FullName, Path.Combine(apkDir, "mapping.txt"), overwrite: true);
                Console.WriteLine("ProGuard mapping.txt copied.");
            }

            // Run jadx
            Console.WriteLine("Decompiling with jadx...");
            var outputDir = Path.Combine(apkDir, "decompiled");
            var runner = new JadxRunner(jadxPath);
            var result = await runner.RunAsync(destApk, outputDir).ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine(CultureInfo.InvariantCulture, $"Decompilation complete: {result.FileCount} Java files in {result.DurationMs}ms");
                Console.WriteLine($"Output: {result.OutputDirectory}");
            }
            else
            {
                Console.WriteLine($"Decompilation failed: {result.ErrorMessage}");
            }
        });

        return decompileCmd;
    }

    private static Command CreateAnalyzeCmd(IServiceProvider services)
    {
        var analyzeCmd = new Command("analyze", "Analyze decompiled APK source");
        var projectOption = new Option<string>("--project") { Description = "Project name", Required = true };

        analyzeCmd.Add(projectOption);

        analyzeCmd.SetAction(async (parseResult) =>
        {
            var project = parseResult.GetRequiredValue(projectOption);

            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var config = await projectStore.LoadAsync(project).ConfigureAwait(false);

            if (config is null)
            {
                Console.WriteLine($"Project '{project}' not found.");
                return;
            }

            var projectDir = projectStore.GetProjectDirectory(project);
            var decompiledDir = Path.Combine(projectDir, "apk", "decompiled");
            var resourcesDir = Path.Combine(projectDir, "apk", "resources");

            if (!Directory.Exists(decompiledDir))
            {
                Console.WriteLine("No decompiled source found. Run 'iaet apk decompile' first.");
                return;
            }

            Console.WriteLine("Analyzing decompiled source...");

            // URL extraction
            Console.WriteLine("  Extracting API endpoints...");
            var urls = ApkUrlExtractor.ExtractFromDirectory(decompiledDir);
            Console.WriteLine(CultureInfo.InvariantCulture, $"  Found {urls.Count} API URLs");

            // Auth extraction
            Console.WriteLine("  Extracting auth patterns...");
            var authEntries = new List<ApkAuthExtractor.AuthEntry>();
            foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                var relativePath = Path.GetRelativePath(decompiledDir, file);
                authEntries.AddRange(ApkAuthExtractor.Extract(source, relativePath));
            }
            Console.WriteLine(CultureInfo.InvariantCulture, $"  Found {authEntries.Count} auth entries");

            // Manifest analysis
            var manifestPath = Path.Combine(resourcesDir, "AndroidManifest.xml");
            var manifest = ManifestAnalyzer.ParseFile(manifestPath);
            if (manifest.PackageName != "unknown")
            {
                Console.WriteLine(CultureInfo.InvariantCulture, $"  Package: {manifest.PackageName} v{manifest.VersionName}");
                Console.WriteLine(CultureInfo.InvariantCulture, $"  Permissions: {manifest.Permissions.Count}");
            }

            // Network security
            var nscPath = Path.Combine(resourcesDir, "res", "xml", "network_security_config.xml");
            var netSecurity = NetworkSecurityAnalyzer.ParseFile(nscPath);

            // Write knowledge
            var knowledgeDir = Path.Combine(projectDir, "knowledge");
            Directory.CreateDirectory(knowledgeDir);

            // endpoints.json
            var endpointsObj = new
            {
                endpoints = urls.Select(u => new
                {
                    signature = u.HttpMethod is not null ? $"{u.HttpMethod} {u.Url}" : u.Url,
                    confidence = u.Confidence.ToString().ToLowerInvariant(),
                    source = u.SourceFile,
                    context = u.Context,
                }).ToList(),
            };
            await File.WriteAllTextAsync(
                Path.Combine(knowledgeDir, "endpoints.json"),
                JsonSerializer.Serialize(endpointsObj, JsonOptions)).ConfigureAwait(false);

            // permissions.json
            var permObj = new
            {
                packageName = manifest.PackageName,
                versionName = manifest.VersionName,
                minSdk = manifest.MinSdk,
                targetSdk = manifest.TargetSdk,
                permissions = manifest.Permissions,
                exportedServices = manifest.ExportedServices,
                exportedReceivers = manifest.ExportedReceivers,
            };
            await File.WriteAllTextAsync(
                Path.Combine(knowledgeDir, "permissions.json"),
                JsonSerializer.Serialize(permObj, JsonOptions)).ConfigureAwait(false);

            // network-security.json
            var nsObj = new
            {
                cleartextDefault = netSecurity.CleartextDefaultPermitted,
                cleartextDomains = netSecurity.CleartextPermittedDomains,
                pinnedDomains = netSecurity.PinnedDomains.Select(d => new { d.Domain, d.Pins }),
            };
            await File.WriteAllTextAsync(
                Path.Combine(knowledgeDir, "network-security.json"),
                JsonSerializer.Serialize(nsObj, JsonOptions)).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("Analysis complete. Knowledge base updated.");
            Console.WriteLine(CultureInfo.InvariantCulture, $"  Endpoints:   {urls.Count}");
            Console.WriteLine(CultureInfo.InvariantCulture, $"  Auth:        {authEntries.Count}");
            Console.WriteLine(CultureInfo.InvariantCulture, $"  Permissions: {manifest.Permissions.Count}");
            Console.WriteLine(CultureInfo.InvariantCulture, $"  Pinned:      {netSecurity.PinnedDomains.Count} domains");
        });

        return analyzeCmd;
    }
}
```

- [ ] **Step 3: Create agent prompt**

Create `agents/apk-analyzer.md` — specialist agent for APK analysis. Follows the same format as other agent prompts. Documents available CLI commands (`iaet apk decompile`, `iaet apk analyze`), expected input (APK file path), and output format (knowledge base JSON files). Includes guidance on obfuscated code and when to use apktool vs jadx.

- [ ] **Step 4: Update Program.cs**

Add `using Iaet.Android;`, `services.AddIaetAndroid();`, and `ApkCommand.Create(host.Services)`.

- [ ] **Step 5: Verify build and tests**

Run: `dotnet build Iaet.slnx && dotnet test Iaet.slnx -v quiet`

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Cli/ agents/apk-analyzer.md
git commit -m "feat(cli): add iaet apk decompile|analyze commands and APK analyzer agent"
```

---

## Task 10: Full Integration Verification

- [ ] **Step 1: Full build**

Run: `dotnet build Iaet.slnx`
Expected: 0 errors.

- [ ] **Step 2: Full test suite**

Run: `dotnet test Iaet.slnx -v quiet`
Expected: All assemblies pass.

- [ ] **Step 3: Verify new test counts**

- ApkUrlExtractorTests: 7
- ApkAuthExtractorTests: 4
- ManifestAnalyzerTests: 4
- NetworkSecurityAnalyzerTests: 3
- JadxRunnerTests: 3
- ApktoolRunnerTests: 2
- ApkModelTests: 2

Total new: ~25 tests.

- [ ] **Step 4: Final commit if fixups needed**

```bash
git add -A
git commit -m "fix: integration fixups from Android Phase 1 testing"
```

---

## Deferred to Phase 2

1. **BleServiceExtractor** — BLE GATT UUID discovery from source
2. **BleOperationExtractor** — read/write/notify operation discovery
3. **BleSigLookup** — standard UUID name table
4. **BleDataFlowTracer** — characteristic → UI tracing
5. **HciLogImporter** — btsnoop binary parser
6. **Dashboard Bluetooth tab** — BLE service visualization
