# Deep Field Resolver — Design Specification

**Date:** 2026-04-01
**Status:** Draft
**Author:** Mark + Claude

## Overview

A multi-signal field resolver that transforms opaque protojson positional arrays (`Field0, Field1, Items2...`) into meaningful named schemas by combining endpoint context, value pattern analysis, recursive nesting, cross-endpoint correlation, and APK source code tracing.

## Problem

Google Voice `account/get` returns:
```json
[["+19196706660", null, [...devices...], [...settings...], [...billing...], ...]]
```

Current output: `InferredResponse(JsonElement[]? Items)` — useless.

Desired output:
```csharp
public sealed record AccountInfo(
    string PhoneNumber,          // "+19196706660" (high confidence)
    JsonElement? Field1,         // null — unknown
    AccountDevices Devices,      // 5 registered devices
    AccountSettings Settings,    // call/SMS feature flags
    BillingInfo Billing,         // USD, $1.70 balance, topup options
    ...
);

public sealed record AccountDevices(
    DeviceEntry[] Items          // each: hash, type, name ("Android Device", "Web")
);

public sealed record BillingInfo(
    CurrencyAmount Balance,      // USD 170000000 (micro-units)
    CurrencyAmount[] TopupOptions, // $10, $25, $50
    string[] SupportedCurrencies // USD, EUR, GBP, CAD
);
```

## Architecture

### FieldResolver pipeline

```
Response Body → RecursiveProtojsonAnalyzer → EndpointContextEnricher
    → ValuePatternMatcher → CrossEndpointCorrelator → [APK SourceTracer]
    → ResolvedSchema
```

Each stage adds evidence. Evidence is accumulated per field position with confidence scores. The final schema uses the highest-confidence name for each field.

### Phase A: Deep nesting + endpoint-aware inference

**RecursiveProtojsonAnalyzer** — extends `ProtojsonAnalyzer` to recursively analyze nested arrays, producing a tree of `ResolvedField` records instead of flat positions.

**EndpointContextEnricher** — uses the endpoint path to generate domain-specific candidate names:
- `/voice/v1/voiceclient/account/get` → "account" domain → candidates: phoneNumber, email, displayName, settings, devices, billing, etc.
- `/voice/v1/voiceclient/sipregisterinfo/get` → "SIP registration" domain → candidates: sipServer, wsUrl, realm, nonce, credentials, etc.
- `/$rpc/google.internal.communications.instantmessaging.v1.Messaging/SendMessage` → "messaging" domain → candidates: messageId, threadId, body, timestamp, sender, recipient, etc.

**Enhanced ValuePatternMatcher** — extends existing `ValueTypeInferrer` to also detect:
- Micro-unit currency amounts (large integers like 170000000 that are 1.70 × 10^8)
- Device identifiers (64-char hex hashes)
- Repeated structures (arrays of same-shaped objects = list of entities)
- Enum-like small integers with known ranges (1=Android, 2=iOS, 3=Web)
- Country/region lists (["US", "CA", "PR", "VI"])
- SIP URIs (`sip:...`, `wss://...telephony...`)

### Phase B: Cross-endpoint value correlation

**CrossEndpointCorrelator** — tracks values across all endpoints in a session:
- Value X appears in `account/get` response field[0] AND in SIP REGISTER as the phone number → field[0] = "phoneNumber" (high confidence)
- Value Y appears in `sipregisterinfo/get` response AND as the WebSocket URL in captured streams → "sipWebSocketUrl" (high confidence)
- Token Z appears in one endpoint's response and another's request header → "authToken" (high confidence)

### Phase C: APK source code correlation

**ProtoFieldMapper** — scans decompiled Java for proto-generated code patterns:
- `response.get(0)` or `response[0]` → tells us code reads field 0
- Variable assignment: `String phoneNumber = response.get(0).getAsString()` → field 0 = "phoneNumber"
- Even obfuscated: `String a = b.get(0).getAsString()` + `this.c = a` + layout uses `c` → trace through

## Data Model

```csharp
public sealed record ResolvedField
{
    public required int Position { get; init; }
    public required string DataType { get; init; }          // string, integer, array, object, etc.
    public string? ResolvedName { get; init; }               // best guess name
    public string? SemanticType { get; init; }               // email, phone, url, currency, etc.
    public ConfidenceLevel Confidence { get; init; }
    public IReadOnlyList<FieldEvidence> Evidence { get; init; } = [];
    public IReadOnlyList<ResolvedField>? NestedFields { get; init; }  // for arrays/objects
    public bool IsRepeatedEntity { get; init; }               // array of same-shaped items
    public string? EntityTypeName { get; init; }              // "Device", "TopupOption", etc.
}

public sealed record FieldEvidence
{
    public required string Source { get; init; }              // "value_pattern", "endpoint_context", "cross_endpoint", "source_code"
    public required string SuggestedName { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public string? Reasoning { get; init; }
}
```

## Implementation Phases

### Phase A: Deep nesting + endpoint-aware inference (this PR)

1. **RecursiveProtojsonAnalyzer** — recurse into nested arrays, detect repeated entities
2. **EndpointContextEnricher** — parse endpoint path for domain hints
3. **Enhanced ValuePatternMatcher** — new patterns (currency micro-units, device hashes, SIP URIs, country lists, enum detection)
4. Wire into `JsonSchemaInferrer` to replace flat `Items` with rich nested types
5. Generate nested C# records for complex structures

### Phase B: Cross-endpoint correlation (future)

6. **CrossEndpointCorrelator** — value tracking across all session endpoints
7. Knowledge base enrichment with cross-reference data

### Phase C: APK source correlation (future)

8. **ProtoFieldMapper** — decompiled source analysis for field position → name mapping
