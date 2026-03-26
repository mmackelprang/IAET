# IAET Phase 3: Schema Inference + HTTP Replay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the schema inference engine (JSON → JSON Schema + C# records + OpenAPI fragments) and the HTTP replay engine (re-issue captured requests, diff responses) with CLI commands for both.

**Architecture:** `Iaet.Schema` implements `ISchemaInferrer` using `System.Text.Json` for structural analysis and string templates for code generation. `Iaet.Replay` implements `IReplayEngine` using `IHttpClientFactory` with Polly resilience, a pluggable auth provider, and JSON diff comparison. Both are leaf assemblies depending only on `Iaet.Core`. CLI commands added for `iaet schema infer/show` and `iaet replay run/batch`.

**Tech Stack:** .NET 10, System.Text.Json, IHttpClientFactory + Polly, System.Threading.RateLimiting, xUnit + FluentAssertions + NSubstitute

**Spec:** See `docs/superpowers/specs/2026-03-26-iaet-standalone-design.md` Sections 5.1, 5.2

**IMPORTANT:** All work on branch `phase3-schema-replay`. Create PR to main when complete. Run comprehensive code review before merging.

---

## Phase 3 Scope

By the end of this phase:
- `ISchemaInferrer` implemented — JSON bodies → merged JSON Schema, C# record, OpenAPI fragment, conflict warnings
- `IReplayEngine` implemented — re-issue captured request, field-level JSON diff, rate limiting
- `IReplayAuthProvider` interface for pluggable credential injection
- Dry-run mode for replay
- `iaet schema infer --session-id <id> --endpoint <signature>` CLI command
- `iaet schema show --session-id <id> --endpoint <signature>` CLI command
- `iaet replay run --request-id <id>` CLI command
- `iaet replay batch --session-id <id>` CLI command
- Tests for all components
- `IEndpointCatalog` extended with `GetResponseBodiesAsync`

---

## File Map

| File | Action | Purpose |
|---|---|---|
| **Schema** | | |
| `src/Iaet.Core/Abstractions/IEndpointCatalog.cs` | Modify | Add `GetResponseBodiesAsync` |
| `src/Iaet.Catalog/SqliteCatalog.cs` | Modify | Implement `GetResponseBodiesAsync` |
| `src/Iaet.Schema/Iaet.Schema.csproj` | Modify | Add NJsonSchema package |
| `src/Iaet.Schema/JsonTypeMap.cs` | Create | Structural type analysis of JSON bodies |
| `src/Iaet.Schema/TypeMerger.cs` | Create | Merge multiple type maps, detect conflicts |
| `src/Iaet.Schema/Generators/JsonSchemaGenerator.cs` | Create | Generate JSON Schema draft-07 |
| `src/Iaet.Schema/Generators/CSharpRecordGenerator.cs` | Create | Generate C# record definitions |
| `src/Iaet.Schema/Generators/OpenApiSchemaGenerator.cs` | Create | Generate OpenAPI 3.1 fragment |
| `src/Iaet.Schema/JsonSchemaInferrer.cs` | Create | `ISchemaInferrer` implementation orchestrator |
| `src/Iaet.Schema/ServiceCollectionExtensions.cs` | Create | DI registration |
| `tests/Iaet.Schema.Tests/JsonTypeMapTests.cs` | Create | Type map parsing tests |
| `tests/Iaet.Schema.Tests/TypeMergerTests.cs` | Create | Merge + conflict detection tests |
| `tests/Iaet.Schema.Tests/Generators/JsonSchemaGeneratorTests.cs` | Create | JSON Schema output tests |
| `tests/Iaet.Schema.Tests/Generators/CSharpRecordGeneratorTests.cs` | Create | C# generation tests |
| `tests/Iaet.Schema.Tests/Generators/OpenApiSchemaGeneratorTests.cs` | Create | OpenAPI output tests |
| `tests/Iaet.Schema.Tests/JsonSchemaInferrerTests.cs` | Create | End-to-end inference tests |
| **Replay** | | |
| `src/Iaet.Core/Abstractions/IReplayAuthProvider.cs` | Create | Auth provider interface |
| `src/Iaet.Replay/Iaet.Replay.csproj` | Modify | Add HTTP + Polly packages |
| `src/Iaet.Replay/HttpReplayEngine.cs` | Create | `IReplayEngine` implementation |
| `src/Iaet.Replay/JsonDiffer.cs` | Create | Field-level JSON comparison |
| `src/Iaet.Replay/ReplayOptions.cs` | Create | Rate limits, dry-run, timeout |
| `src/Iaet.Replay/ServiceCollectionExtensions.cs` | Create | DI registration |
| `tests/Iaet.Replay.Tests/JsonDifferTests.cs` | Create | JSON diff tests |
| `tests/Iaet.Replay.Tests/HttpReplayEngineTests.cs` | Create | Replay + diff integration tests |
| **CLI** | | |
| `src/Iaet.Cli/Commands/SchemaCommand.cs` | Create | `iaet schema infer/show` |
| `src/Iaet.Cli/Commands/ReplayCommand.cs` | Create | `iaet replay run/batch` |
| `src/Iaet.Cli/Iaet.Cli.csproj` | Modify | Add Iaet.Schema + Iaet.Replay refs |
| `src/Iaet.Cli/Program.cs` | Modify | Register new commands + DI |

---

## Task 1: Create Branch and Extend IEndpointCatalog

**Files:**
- Modify: `src/Iaet.Core/Abstractions/IEndpointCatalog.cs`
- Modify: `src/Iaet.Catalog/SqliteCatalog.cs`
- Modify: `tests/Iaet.Catalog.Tests/SqliteCatalogTests.cs`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/prj/IAET
git checkout main && git pull
git checkout -b phase3-schema-replay
```

- [ ] **Step 2: Add GetResponseBodiesAsync and GetRequestByIdAsync to IEndpointCatalog**

Schema inference needs all response bodies for a given endpoint. Replay needs to fetch a single request by ID. Add both to `IEndpointCatalog`:

```csharp
Task<IReadOnlyList<string>> GetResponseBodiesAsync(
    Guid sessionId, string normalizedSignature, CancellationToken ct = default);

Task<CapturedRequest?> GetRequestByIdAsync(Guid requestId, CancellationToken ct = default);
```

Implement both in `SqliteCatalog`. `GetRequestByIdAsync` queries `_db.Requests` by `Id`, deserializes headers, returns the first match or null.

- [ ] **Step 3: Write test for the new method (TDD)**

Add to `tests/Iaet.Catalog.Tests/SqliteCatalogTests.cs`:

```csharp
[Fact]
public async Task GetResponseBodiesAsync_ReturnsNonNullBodies()
{
    var sessionId = Guid.NewGuid();
    // ... seed session and two requests with same normalized endpoint
    // ... one with responseBody = '{"status":"ok"}', one with responseBody = '{"status":"error","code":500}'

    var bodies = await _catalog.GetResponseBodiesAsync(sessionId, "GET /api/users/{id}");

    bodies.Should().HaveCount(2);
    bodies.Should().Contain(b => b.Contains("ok"));
    bodies.Should().Contain(b => b.Contains("error"));
}

[Fact]
public async Task GetResponseBodiesAsync_ExcludesNullBodies()
{
    // ... request with null responseBody
    var bodies = await _catalog.GetResponseBodiesAsync(sessionId, "GET /api/users/{id}");
    bodies.Should().BeEmpty();
}
```

- [ ] **Step 4: Implement in SqliteCatalog**

```csharp
public async Task<IReadOnlyList<string>> GetResponseBodiesAsync(
    Guid sessionId, string normalizedSignature, CancellationToken ct = default)
{
    var bodies = await _db.Requests
        .Where(r => r.SessionId == sessionId
            && r.NormalizedSignature == normalizedSignature
            && r.ResponseBody != null)
        .Select(r => r.ResponseBody!)
        .ToListAsync(ct).ConfigureAwait(false);
    return bodies;
}
```

- [ ] **Step 5: Run tests, commit**

```bash
dotnet test Iaet.slnx -v n
git add src/Iaet.Core/ src/Iaet.Catalog/ tests/Iaet.Catalog.Tests/
git commit -m "feat: add GetResponseBodiesAsync to IEndpointCatalog for schema inference"
```

---

## Task 2: Iaet.Schema — JsonTypeMap (TDD)

The type map is the core data structure — it represents the structural type of a JSON value. Given a JSON string, it produces a tree of field names → observed types.

**Files:**
- Create: `src/Iaet.Schema/JsonTypeMap.cs`
- Create: `tests/Iaet.Schema.Tests/JsonTypeMapTests.cs`

- [ ] **Step 1: Set up Schema project**

```bash
dotnet add src/Iaet.Schema package NJsonSchema
dotnet new xunit -n Iaet.Schema.Tests -o tests/Iaet.Schema.Tests
rm tests/Iaet.Schema.Tests/UnitTest1.cs
dotnet sln Iaet.slnx add tests/Iaet.Schema.Tests/Iaet.Schema.Tests.csproj
dotnet add tests/Iaet.Schema.Tests reference src/Iaet.Schema
dotnet add tests/Iaet.Schema.Tests reference src/Iaet.Core
dotnet add tests/Iaet.Schema.Tests package FluentAssertions
dotnet add tests/Iaet.Schema.Tests package NSubstitute
```

Strip redundant csproj properties. Remove Placeholder.cs from src/Iaet.Schema/.

- [ ] **Step 2: Write JsonTypeMap tests**

```csharp
using FluentAssertions;
using Iaet.Schema;

namespace Iaet.Schema.Tests;

public class JsonTypeMapTests
{
    [Fact]
    public void Analyze_SimpleObject_ExtractsFieldTypes()
    {
        var json = """{"name":"Alice","age":30,"active":true}""";
        var map = JsonTypeMap.Analyze(json);

        map.Fields.Should().HaveCount(3);
        map.Fields["name"].JsonType.Should().Be(JsonFieldType.String);
        map.Fields["age"].JsonType.Should().Be(JsonFieldType.Number);
        map.Fields["active"].JsonType.Should().Be(JsonFieldType.Boolean);
    }

    [Fact]
    public void Analyze_NestedObject_CreatesNestedMap()
    {
        var json = """{"user":{"id":1,"name":"Bob"}}""";
        var map = JsonTypeMap.Analyze(json);

        map.Fields["user"].JsonType.Should().Be(JsonFieldType.Object);
        map.Fields["user"].NestedFields.Should().ContainKey("id");
        map.Fields["user"].NestedFields!["id"].JsonType.Should().Be(JsonFieldType.Number);
    }

    [Fact]
    public void Analyze_Array_InfersItemType()
    {
        var json = """{"tags":["a","b","c"]}""";
        var map = JsonTypeMap.Analyze(json);

        map.Fields["tags"].JsonType.Should().Be(JsonFieldType.Array);
        map.Fields["tags"].ArrayItemType.Should().NotBeNull();
        map.Fields["tags"].ArrayItemType!.JsonType.Should().Be(JsonFieldType.String);
    }

    [Fact]
    public void Analyze_NullField_MarksAsNull()
    {
        var json = """{"value":null}""";
        var map = JsonTypeMap.Analyze(json);

        map.Fields["value"].JsonType.Should().Be(JsonFieldType.Null);
    }

    [Fact]
    public void Analyze_EmptyObject_HasNoFields()
    {
        var map = JsonTypeMap.Analyze("{}");
        map.Fields.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_ArrayOfObjects_InfersObjectSchema()
    {
        var json = """{"items":[{"id":1},{"id":2,"extra":"val"}]}""";
        var map = JsonTypeMap.Analyze(json);

        map.Fields["items"].ArrayItemType.Should().NotBeNull();
        map.Fields["items"].ArrayItemType!.JsonType.Should().Be(JsonFieldType.Object);
        // Should have merged fields from both array items
        map.Fields["items"].ArrayItemType!.NestedFields.Should().ContainKey("id");
    }
}
```

- [ ] **Step 3: Run tests to verify failure, then implement**

Create `src/Iaet.Schema/JsonTypeMap.cs`:

```csharp
using System.Text.Json;

namespace Iaet.Schema;

public enum JsonFieldType
{
    String, Number, Boolean, Null, Object, Array
}

public sealed record FieldInfo
{
    public required JsonFieldType JsonType { get; init; }
    public Dictionary<string, FieldInfo>? NestedFields { get; init; }
    public FieldInfo? ArrayItemType { get; init; }
    public bool IsNullable { get; init; }
}

public sealed class JsonTypeMap
{
    public Dictionary<string, FieldInfo> Fields { get; } = new(StringComparer.Ordinal);

    public static JsonTypeMap Analyze(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var map = new JsonTypeMap();
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                map.Fields[prop.Name] = AnalyzeElement(prop.Value);
            }
        }
        return map;
    }

    private static FieldInfo AnalyzeElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => new FieldInfo { JsonType = JsonFieldType.String },
            JsonValueKind.Number => new FieldInfo { JsonType = JsonFieldType.Number },
            JsonValueKind.True or JsonValueKind.False => new FieldInfo { JsonType = JsonFieldType.Boolean },
            JsonValueKind.Null => new FieldInfo { JsonType = JsonFieldType.Null, IsNullable = true },
            JsonValueKind.Object => AnalyzeObject(element),
            JsonValueKind.Array => AnalyzeArray(element),
            _ => new FieldInfo { JsonType = JsonFieldType.String }
        };
    }

    private static FieldInfo AnalyzeObject(JsonElement element)
    {
        var nested = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
        foreach (var prop in element.EnumerateObject())
        {
            nested[prop.Name] = AnalyzeElement(prop.Value);
        }
        return new FieldInfo { JsonType = JsonFieldType.Object, NestedFields = nested };
    }

    private static FieldInfo AnalyzeArray(JsonElement element)
    {
        FieldInfo? itemType = null;
        foreach (var item in element.EnumerateArray())
        {
            var currentType = AnalyzeElement(item);
            itemType = itemType is null ? currentType : MergeFieldInfo(itemType, currentType);
        }
        return new FieldInfo { JsonType = JsonFieldType.Array, ArrayItemType = itemType };
    }

    internal static FieldInfo MergeFieldInfo(FieldInfo a, FieldInfo b)
    {
        // If same type, merge nested fields
        if (a.JsonType == b.JsonType && a.JsonType == JsonFieldType.Object
            && a.NestedFields is not null && b.NestedFields is not null)
        {
            var merged = new Dictionary<string, FieldInfo>(a.NestedFields, StringComparer.Ordinal);
            foreach (var (key, value) in b.NestedFields)
            {
                if (merged.TryGetValue(key, out var existing))
                    merged[key] = MergeFieldInfo(existing, value);
                else
                    merged[key] = value;
            }
            return new FieldInfo { JsonType = JsonFieldType.Object, NestedFields = merged };
        }

        // If one is null, take the other but mark nullable
        if (a.JsonType == JsonFieldType.Null) return b with { IsNullable = true };
        if (b.JsonType == JsonFieldType.Null) return a with { IsNullable = true };

        // Type conflict — keep first, mark nullable
        return a with { IsNullable = true };
    }
}
```

- [ ] **Step 4: Run tests, commit**

```bash
dotnet test tests/Iaet.Schema.Tests -v n
git add src/Iaet.Schema/ tests/Iaet.Schema.Tests/
git commit -m "feat: add JsonTypeMap for structural JSON type analysis"
```

---

## Task 3: Iaet.Schema — TypeMerger (TDD)

Merges multiple `JsonTypeMap` instances and detects conflicts.

**Files:**
- Create: `src/Iaet.Schema/TypeMerger.cs`
- Create: `tests/Iaet.Schema.Tests/TypeMergerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
public class TypeMergerTests
{
    [Fact]
    public void Merge_IdenticalSchemas_ProducesCleanResult()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"name":"Alice","age":30}"""),
            JsonTypeMap.Analyze("""{"name":"Bob","age":25}""")
        };

        var result = TypeMerger.Merge(maps);

        result.MergedMap.Fields["name"].JsonType.Should().Be(JsonFieldType.String);
        result.MergedMap.Fields["age"].JsonType.Should().Be(JsonFieldType.Number);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Merge_NullableField_MarksAsNullable()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"status":"ok"}"""),
            JsonTypeMap.Analyze("""{"status":null}""")
        };

        var result = TypeMerger.Merge(maps);

        result.MergedMap.Fields["status"].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Merge_TypeConflict_ProducesWarning()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"count":42}"""),
            JsonTypeMap.Analyze("""{"count":"many"}""")
        };

        var result = TypeMerger.Merge(maps);

        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("count");
    }

    [Fact]
    public void Merge_OptionalField_DetectsAbsence()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"id":1,"name":"Alice"}"""),
            JsonTypeMap.Analyze("""{"id":2}""")
        };

        var result = TypeMerger.Merge(maps);

        result.MergedMap.Fields["name"].IsNullable.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Implement TypeMerger**

```csharp
namespace Iaet.Schema;

public sealed record MergeResult(JsonTypeMap MergedMap, IReadOnlyList<string> Warnings);

public static class TypeMerger
{
    public static MergeResult Merge(IReadOnlyList<JsonTypeMap> maps)
    {
        // Implementation: iterate all maps, merge field-by-field,
        // track warnings for type conflicts and missing fields
    }
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test tests/Iaet.Schema.Tests -v n
git add src/Iaet.Schema/ tests/Iaet.Schema.Tests/
git commit -m "feat: add TypeMerger for multi-sample schema merging with conflict detection"
```

---

## Task 4: Iaet.Schema — Generators (TDD)

Three generators that produce output from a merged type map.

**Files:**
- Create: `src/Iaet.Schema/Generators/JsonSchemaGenerator.cs`
- Create: `src/Iaet.Schema/Generators/CSharpRecordGenerator.cs`
- Create: `src/Iaet.Schema/Generators/OpenApiSchemaGenerator.cs`
- Create: `tests/Iaet.Schema.Tests/Generators/JsonSchemaGeneratorTests.cs`
- Create: `tests/Iaet.Schema.Tests/Generators/CSharpRecordGeneratorTests.cs`
- Create: `tests/Iaet.Schema.Tests/Generators/OpenApiSchemaGeneratorTests.cs`

- [ ] **Step 1: Write JsonSchemaGenerator tests**

```csharp
public class JsonSchemaGeneratorTests
{
    [Fact]
    public void Generate_SimpleObject_ProducesValidSchema()
    {
        var map = JsonTypeMap.Analyze("""{"name":"Alice","age":30}""");
        var schema = JsonSchemaGenerator.Generate(map);

        schema.Should().Contain("\"type\":\"object\"");
        schema.Should().Contain("\"name\"");
        schema.Should().Contain("\"string\"");
        schema.Should().Contain("\"number\"");

        // Validate it parses as valid JSON
        var doc = System.Text.Json.JsonDocument.Parse(schema);
        doc.Should().NotBeNull();
    }

    [Fact]
    public void Generate_NullableField_UsesOneOf()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"value":"hello"}"""),
            JsonTypeMap.Analyze("""{"value":null}""")
        };
        var merged = TypeMerger.Merge(maps).MergedMap;
        var schema = JsonSchemaGenerator.Generate(merged);

        // Nullable fields should produce type array or oneOf with null
        schema.Should().Contain("null");
    }
}
```

- [ ] **Step 2: Write CSharpRecordGenerator tests**

```csharp
public class CSharpRecordGeneratorTests
{
    [Fact]
    public void Generate_SimpleObject_ProducesRecord()
    {
        var map = JsonTypeMap.Analyze("""{"userName":"Alice","age":30,"isActive":true}""");
        var code = CSharpRecordGenerator.Generate(map, "UserResponse");

        code.Should().Contain("public sealed record UserResponse");
        code.Should().Contain("string UserName");
        code.Should().Contain("double Age"); // JSON numbers → double
        code.Should().Contain("bool IsActive");
    }

    [Fact]
    public void Generate_NullableField_UseNullableType()
    {
        var maps = new[]
        {
            JsonTypeMap.Analyze("""{"name":"Alice"}"""),
            JsonTypeMap.Analyze("""{"name":null}""")
        };
        var merged = TypeMerger.Merge(maps).MergedMap;
        var code = CSharpRecordGenerator.Generate(merged, "Response");

        code.Should().Contain("string?");
    }

    [Fact]
    public void Generate_NestedObject_ProducesNestedRecord()
    {
        var map = JsonTypeMap.Analyze("""{"user":{"id":1,"name":"Bob"}}""");
        var code = CSharpRecordGenerator.Generate(map, "Response");

        code.Should().Contain("record Response");
        code.Should().Contain("record User"); // nested record
    }
}
```

- [ ] **Step 3: Write OpenApiSchemaGenerator tests**

```csharp
public class OpenApiSchemaGeneratorTests
{
    [Fact]
    public void Generate_SimpleObject_ProducesYaml()
    {
        var map = JsonTypeMap.Analyze("""{"name":"Alice","age":30}""");
        var yaml = OpenApiSchemaGenerator.Generate(map);

        yaml.Should().Contain("type: object");
        yaml.Should().Contain("name:");
        yaml.Should().Contain("type: string");
        yaml.Should().Contain("type: number");
    }
}
```

- [ ] **Step 4: Implement all three generators, run tests**

JsonSchemaGenerator: Build a JSON Schema draft-07 object from the type map. Use `System.Text.Json` to write the schema (not NJsonSchema dependency for output — keep it simple with manual construction).

CSharpRecordGenerator: Build C# record source from the type map using string builder. Convert field names to PascalCase. Map types: String→string, Number→double, Boolean→bool, Array→IReadOnlyList<T>, Object→nested record.

OpenApiSchemaGenerator: Build YAML string from the type map. Simple indented text output (no YAML library needed for generation).

- [ ] **Step 5: Run tests, commit**

```bash
dotnet test tests/Iaet.Schema.Tests -v n
git add src/Iaet.Schema/ tests/Iaet.Schema.Tests/
git commit -m "feat: add JSON Schema, C# record, and OpenAPI generators"
```

---

## Task 5: Iaet.Schema — JsonSchemaInferrer + DI (TDD)

The orchestrator that ties type analysis, merging, and generation together.

**Files:**
- Create: `src/Iaet.Schema/JsonSchemaInferrer.cs`
- Create: `src/Iaet.Schema/ServiceCollectionExtensions.cs`
- Create: `tests/Iaet.Schema.Tests/JsonSchemaInferrerTests.cs`

- [ ] **Step 1: Write end-to-end tests**

```csharp
public class JsonSchemaInferrerTests
{
    [Fact]
    public async Task InferAsync_MultipleBodies_ProducesAllOutputs()
    {
        var inferrer = new JsonSchemaInferrer();
        var bodies = new[]
        {
            """{"id":1,"name":"Alice","email":"alice@example.com"}""",
            """{"id":2,"name":"Bob","email":null}"""
        };

        var result = await inferrer.InferAsync(bodies);

        result.JsonSchema.Should().NotBeNullOrWhiteSpace();
        result.CSharpRecord.Should().Contain("record");
        result.OpenApiFragment.Should().Contain("type: object");
        result.Warnings.Should().BeEmpty(); // no type conflicts
    }

    [Fact]
    public async Task InferAsync_TypeConflict_ReportsWarning()
    {
        var inferrer = new JsonSchemaInferrer();
        var bodies = new[]
        {
            """{"count":42}""",
            """{"count":"unknown"}"""
        };

        var result = await inferrer.InferAsync(bodies);

        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InferAsync_EmptyBodies_ReturnsEmptySchema()
    {
        var inferrer = new JsonSchemaInferrer();
        var result = await inferrer.InferAsync([]);

        result.JsonSchema.Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 2: Implement and add DI registration**

```csharp
public sealed class JsonSchemaInferrer : ISchemaInferrer
{
    public Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default)
    {
        if (jsonBodies.Count == 0)
            return Task.FromResult(new SchemaResult("{}", "", "", []));

        var maps = jsonBodies.Select(JsonTypeMap.Analyze).ToList();
        var merged = TypeMerger.Merge(maps);

        var jsonSchema = JsonSchemaGenerator.Generate(merged.MergedMap);
        var csharp = CSharpRecordGenerator.Generate(merged.MergedMap, "InferredResponse");
        var openApi = OpenApiSchemaGenerator.Generate(merged.MergedMap);

        return Task.FromResult(new SchemaResult(jsonSchema, csharp, openApi, merged.Warnings));
    }
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test tests/Iaet.Schema.Tests -v n
git add src/Iaet.Schema/ tests/Iaet.Schema.Tests/
git commit -m "feat: add JsonSchemaInferrer orchestrating type analysis, merging, and generation"
```

---

## Task 6: Iaet.Replay — JsonDiffer (TDD)

Field-level JSON comparison engine.

**Files:**
- Create: `src/Iaet.Replay/JsonDiffer.cs`
- Create: `tests/Iaet.Replay.Tests/JsonDifferTests.cs`

- [ ] **Step 1: Set up Replay project**

```bash
dotnet add src/Iaet.Replay package Microsoft.Extensions.Http
dotnet add src/Iaet.Replay package Microsoft.Extensions.Http.Resilience
dotnet add src/Iaet.Replay package Microsoft.Extensions.Options
dotnet add src/Iaet.Replay package System.Threading.RateLimiting
dotnet add src/Iaet.Replay package Microsoft.Extensions.Logging.Abstractions
dotnet new xunit -n Iaet.Replay.Tests -o tests/Iaet.Replay.Tests
rm tests/Iaet.Replay.Tests/UnitTest1.cs
dotnet sln Iaet.slnx add tests/Iaet.Replay.Tests/Iaet.Replay.Tests.csproj
dotnet add tests/Iaet.Replay.Tests reference src/Iaet.Replay
dotnet add tests/Iaet.Replay.Tests reference src/Iaet.Core
dotnet add tests/Iaet.Replay.Tests package FluentAssertions
dotnet add tests/Iaet.Replay.Tests package NSubstitute
```

Remove Placeholder.cs from src/Iaet.Replay/.

- [ ] **Step 2: Write JsonDiffer tests**

```csharp
public class JsonDifferTests
{
    [Fact]
    public void Diff_IdenticalJson_NoDiffs()
    {
        var diffs = JsonDiffer.Diff("""{"a":1}""", """{"a":1}""");
        diffs.Should().BeEmpty();
    }

    [Fact]
    public void Diff_ChangedValue_ReportsChange()
    {
        var diffs = JsonDiffer.Diff("""{"a":1}""", """{"a":2}""");
        diffs.Should().ContainSingle()
            .Which.Path.Should().Be("$.a");
    }

    [Fact]
    public void Diff_AddedField_ReportsAddition()
    {
        var diffs = JsonDiffer.Diff("""{"a":1}""", """{"a":1,"b":2}""");
        diffs.Should().ContainSingle()
            .Which.Expected.Should().BeNull(); // field didn't exist in expected
    }

    [Fact]
    public void Diff_RemovedField_ReportsRemoval()
    {
        var diffs = JsonDiffer.Diff("""{"a":1,"b":2}""", """{"a":1}""");
        diffs.Should().ContainSingle()
            .Which.Actual.Should().BeNull();
    }

    [Fact]
    public void Diff_NestedChange_ReportsFullPath()
    {
        var diffs = JsonDiffer.Diff(
            """{"user":{"name":"Alice"}}""",
            """{"user":{"name":"Bob"}}""");
        diffs.Should().ContainSingle()
            .Which.Path.Should().Be("$.user.name");
    }

    [Fact]
    public void Diff_NullInputs_HandlesGracefully()
    {
        JsonDiffer.Diff(null, null).Should().BeEmpty();
        JsonDiffer.Diff("""{"a":1}""", null).Should().NotBeEmpty();
        JsonDiffer.Diff(null, """{"a":1}""").Should().NotBeEmpty();
    }
}
```

- [ ] **Step 3: Implement JsonDiffer**

Recursive JSON comparison using `System.Text.Json`. Produces `IReadOnlyList<FieldDiff>` using JSONPath-style paths.

- [ ] **Step 4: Run tests, commit**

```bash
dotnet test tests/Iaet.Replay.Tests -v n
git add src/Iaet.Replay/ tests/Iaet.Replay.Tests/
git commit -m "feat: add JsonDiffer for field-level JSON comparison with JSONPath paths"
```

---

## Task 7: Iaet.Replay — HttpReplayEngine + Auth Provider (TDD)

**Files:**
- Create: `src/Iaet.Core/Abstractions/IReplayAuthProvider.cs`
- Create: `src/Iaet.Replay/ReplayOptions.cs`
- Create: `src/Iaet.Replay/HttpReplayEngine.cs`
- Create: `src/Iaet.Replay/ServiceCollectionExtensions.cs`
- Create: `tests/Iaet.Replay.Tests/HttpReplayEngineTests.cs`

- [ ] **Step 1: Create IReplayAuthProvider in Core**

```csharp
namespace Iaet.Core.Abstractions;

public interface IReplayAuthProvider
{
    Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create ReplayOptions**

```csharp
namespace Iaet.Replay;

public sealed class ReplayOptions
{
    public int RequestsPerMinute { get; init; } = 10;
    public int RequestsPerDay { get; init; } = 100;
    public int TimeoutSeconds { get; init; } = 30;
    public bool DryRun { get; init; }
}
```

- [ ] **Step 3: Write HttpReplayEngine tests**

```csharp
public class HttpReplayEngineTests
{
    [Fact]
    public async Task ReplayAsync_MatchingResponse_NoDiffs()
    {
        var handler = new FakeHttpHandler("""{"a":1}""", 200);
        var engine = CreateEngine(handler);
        var request = MakeCapturedRequest("""{"a":1}""", 200);

        var result = await engine.ReplayAsync(request);

        result.ResponseStatus.Should().Be(200);
        result.Diffs.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplayAsync_DifferentResponse_ReportsDiffs()
    {
        var handler = new FakeHttpHandler("""{"a":2}""", 200);
        var engine = CreateEngine(handler);
        var request = MakeCapturedRequest("""{"a":1}""", 200);

        var result = await engine.ReplayAsync(request);

        result.Diffs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReplayAsync_DryRun_DoesNotSendRequest()
    {
        var handler = new FakeHttpHandler("""{}""", 200);
        var engine = CreateEngine(handler, new ReplayOptions { DryRun = true });
        var request = MakeCapturedRequest("""{}""", 200);

        var result = await engine.ReplayAsync(request);

        handler.CallCount.Should().Be(0);
        result.ResponseStatus.Should().Be(0); // no actual response
    }

    [Fact]
    public async Task ReplayAsync_WithAuthProvider_AppliesAuth()
    {
        var handler = new FakeHttpHandler("""{}""", 200);
        var authProvider = Substitute.For<IReplayAuthProvider>();
        var engine = CreateEngine(handler, authProvider: authProvider);
        var request = MakeCapturedRequest("""{}""", 200);

        await engine.ReplayAsync(request);

        await authProvider.Received(1).ApplyAuthAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 4: Implement HttpReplayEngine**

```csharp
public sealed class HttpReplayEngine : IReplayEngine
{
    private readonly HttpClient _httpClient;
    private readonly IReplayAuthProvider? _authProvider;
    private readonly ReplayOptions _options;
    private readonly FixedWindowRateLimiter _rateLimiter;

    // Constructor takes HttpClient (from IHttpClientFactory), optional auth provider, options
    // ReplayAsync: build HttpRequestMessage from CapturedRequest, apply auth, send, diff response
    // DryRun: skip sending, return empty result
    // Rate limiting: check before sending
}
```

- [ ] **Step 5: Add DI registration**

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetReplay(this IServiceCollection services,
        Action<ReplayOptions>? configure = null)
    {
        var options = new ReplayOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddHttpClient<IReplayEngine, HttpReplayEngine>()
            .AddStandardResilienceHandler();
        return services;
    }
}
```

- [ ] **Step 6: Run tests, commit**

```bash
dotnet test tests/Iaet.Replay.Tests -v n
git add src/Iaet.Core/ src/Iaet.Replay/ tests/Iaet.Replay.Tests/
git commit -m "feat: add HttpReplayEngine with JSON diff, auth provider, rate limiting, and dry-run"
```

---

## Task 8: CLI Commands for Schema and Replay

**Files:**
- Create: `src/Iaet.Cli/Commands/SchemaCommand.cs`
- Create: `src/Iaet.Cli/Commands/ReplayCommand.cs`
- Modify: `src/Iaet.Cli/Iaet.Cli.csproj`
- Modify: `src/Iaet.Cli/Program.cs`

- [ ] **Step 1: Add project references**

```bash
dotnet add src/Iaet.Cli reference src/Iaet.Schema
dotnet add src/Iaet.Cli reference src/Iaet.Replay
```

- [ ] **Step 2: Create SchemaCommand**

Commands:
- `iaet schema infer --session-id <guid> --endpoint <signature>` — infers schema from all response bodies for that endpoint
- `iaet schema show --session-id <guid> --endpoint <signature> --format <json|csharp|openapi>` — shows specific output format

Each creates DI scope, runs MigrateAsync, resolves `IEndpointCatalog` + `ISchemaInferrer`.

- [ ] **Step 3: Create ReplayCommand**

Commands:
- `iaet replay run --request-id <guid> --session-id <guid>` — replays a single request, shows diff
- `iaet replay batch --session-id <guid>` — replays all endpoints in a session
- `--dry-run` flag on both

Each creates DI scope, resolves `IEndpointCatalog` + `IReplayEngine`.

- [ ] **Step 4: Register in Program.cs**

Add DI registrations and commands:
```csharp
services.AddIaetSchema();
services.AddIaetReplay();
```

```csharp
SchemaCommand.Create(host.Services),
ReplayCommand.Create(host.Services)
```

- [ ] **Step 5: Verify CLI**

```bash
dotnet run --project src/Iaet.Cli -- schema --help
dotnet run --project src/Iaet.Cli -- replay --help
```

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Cli/
git commit -m "feat: add schema infer/show and replay run/batch CLI commands"
```

---

## Task 9: Update Docs + Create PR

- [ ] **Step 1: Update README.md** — move Schema and Replay from "coming" to implemented

- [ ] **Step 2: Add per-assembly READMEs**
- `src/Iaet.Schema/README.md`
- `src/Iaet.Replay/README.md`

- [ ] **Step 3: Run full test suite**

```bash
dotnet test Iaet.slnx -c Release
```

- [ ] **Step 4: Commit, push, create PR**

```bash
git add README.md src/*/README.md
git commit -m "docs: update README and add Schema/Replay assembly docs"
git push origin phase3-schema-replay
```

```bash
gh pr create --title "Phase 3: Schema Inference + HTTP Replay" --body "$(cat <<'EOF'
## Summary

Adds JSON schema inference and HTTP replay with diff engine to IAET:

- **Iaet.Schema** — `ISchemaInferrer` implementation that analyzes JSON response bodies and produces:
  - Merged JSON Schema (draft-07) handling nullable fields and type conflicts
  - C# record definitions (PascalCase, nullable-aware)
  - OpenAPI 3.1 schema fragments
  - Conflict warnings when types disagree across samples
- **Iaet.Replay** — `IReplayEngine` implementation that:
  - Re-issues captured requests via `IHttpClientFactory` with Polly resilience
  - Compares live response to captured baseline with field-level JSON diff (JSONPath)
  - Supports pluggable auth via `IReplayAuthProvider`
  - Rate limiting (10 req/min, 100 req/day defaults)
  - Dry-run mode
- **CLI commands** — `iaet schema infer/show` and `iaet replay run/batch`
- **Catalog extension** — `GetResponseBodiesAsync` for efficient schema inference queries

## Test Plan
- [ ] JsonTypeMap: simple objects, nested objects, arrays, nulls, array-of-objects
- [ ] TypeMerger: identical schemas, nullable fields, type conflicts, optional fields
- [ ] JsonSchemaGenerator: valid schema output, nullable handling
- [ ] CSharpRecordGenerator: simple records, nullable types, nested records
- [ ] OpenApiSchemaGenerator: valid YAML output
- [ ] JsonSchemaInferrer: end-to-end multi-body inference, conflict warnings, empty input
- [ ] JsonDiffer: identical JSON, changed values, added/removed fields, nested paths, null handling
- [ ] HttpReplayEngine: matching response, diff detection, dry-run, auth provider
- [ ] CLI: schema --help, replay --help

Generated with Claude Code
EOF
)"
```

---

## What's Next

After Phase 3, IAET has:
- HTTP + stream capture (Phases 1-2)
- Schema inference from captured JSON (Phase 3)
- HTTP replay with diff (Phase 3)

**Phase 4 (Export + Documentation)** builds on Schema to generate reports, OpenAPI specs, Postman collections, and C# clients.
