# Iaet.Schema

`Iaet.Schema` implements `ISchemaInferrer` to derive typed schema definitions from captured JSON response bodies stored in the IAET catalog.

---

## Purpose

Given a set of raw JSON response bodies observed for a single endpoint, `Iaet.Schema` produces three output formats simultaneously:

- **JSON Schema (draft-07)** — mergeable, nullable-aware object schema
- **C# record definition** — PascalCase property names, nullable value types where applicable
- **OpenAPI 3.1 schema fragment** — ready for embedding in a spec file

Type conflicts across samples (e.g., a field that is `string` in one response and `number` in another) are surfaced as warnings rather than hard errors, so inference is always best-effort.

---

## Key Types

### `ISchemaInferrer` (from `Iaet.Core.Abstractions`)

```csharp
Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default);
```

Returns a `SchemaResult(JsonSchema, CSharpRecord, OpenApiFragment, Warnings)`.

### `JsonTypeMap`

Parses a single JSON string and builds a structural map of field names to `FieldInfo` descriptors (type, nullability, nested fields, array item types).

```csharp
var map = JsonTypeMap.Analyze(jsonBody);
```

### `TypeMerger`

Merges an ordered list of `JsonTypeMap` instances into a single unified map, accumulating conflict warnings.

```csharp
var (mergedMap, warnings) = TypeMerger.Merge(maps);
```

Merging rules:
- If a field appears in only some samples it is marked optional (nullable).
- If a field is `null` in one sample it is marked nullable.
- If a field has incompatible types across samples the first type wins and a warning is emitted.
- Nested objects and array item types are merged recursively.

### Generators

| Class | Output |
|---|---|
| `JsonSchemaGenerator` | JSON Schema draft-07 string |
| `CSharpRecordGenerator` | `public sealed record` definition |
| `OpenApiSchemaGenerator` | OpenAPI 3.1 `schema:` YAML fragment |

---

## Dependency Injection

```csharp
services.AddIaetSchema();
```

Registers `JsonSchemaInferrer` as the singleton `ISchemaInferrer` implementation. No additional configuration is required.

---

## CLI Usage

```bash
# All three formats at once
iaet schema infer --session-id <guid> --endpoint "GET /api/users"

# One format
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format json
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format csharp
iaet schema show --session-id <guid> --endpoint "GET /api/users" --format openapi
```
