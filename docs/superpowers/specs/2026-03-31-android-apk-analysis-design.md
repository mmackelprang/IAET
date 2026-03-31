# Android APK Analysis — Design Specification

**Date:** 2026-03-31
**Status:** Draft
**Author:** Mark + Claude

## Overview

Extend IAET to analyze Android APK files, extracting API endpoints, authentication patterns, and Bluetooth Low Energy (BLE) service definitions from decompiled source code. Treats BLE GATT services as first-class API surfaces alongside REST/gRPC/WebSocket endpoints. Supports obfuscated code through type-based and API-call-based pattern matching.

### Primary Use Case

Developers investigating Android apps to discover and document:
- Undocumented REST/gRPC API endpoints and authentication mechanisms
- BLE GATT services, characteristics, and operations
- Data flow from BLE characteristics through parsing code to UI components
- Network security configuration (certificate pinning, cleartext policy)
- Android permissions and exported service surfaces

### Design Principles

- **Static analysis first** — extract maximum value from APK decompilation without needing a device
- **Obfuscation-aware** — match by SDK types and API calls, not names (ProGuard/R8 strip names but not Android framework signatures)
- **BLE as first-class API** — GATT services map to endpoints, characteristics to operations, just like REST paths map to methods
- **Reuse existing IAET pipeline** — feed discoveries into the same knowledge base, diagrams, and dashboard used by web investigations
- **General-purpose** — nothing in the tooling is app-specific; works on any Android APK

---

## Architecture

### New Assembly: Iaet.Android

```
Iaet.Android
├── Decompilation/
│   ├── JadxRunner.cs              — shell out to jadx CLI
│   └── ApktoolRunner.cs           — shell out to apktool CLI (on demand)
├── Extractors/
│   ├── ApkUrlExtractor.cs         — API endpoint URLs from Java/Kotlin source
│   ├── ApkAuthExtractor.cs        — API keys, auth patterns, OAuth config
│   ├── ManifestAnalyzer.cs        — AndroidManifest.xml permissions, services
│   └── NetworkSecurityAnalyzer.cs — certificate pinning, cleartext config
├── Bluetooth/
│   ├── BleServiceExtractor.cs     — GATT service/characteristic UUIDs from source
│   ├── BleOperationExtractor.cs   — read/write/notify operations
│   ├── BleDataFlowTracer.cs       — characteristic → parsing → UI data flow
│   ├── BleSigLookup.cs            — Bluetooth SIG standard UUID name table
│   └── HciLogImporter.cs          — btsnoop_hci.log binary parser
├── ApkAnalysisResult.cs           — aggregated analysis output
└── ServiceCollectionExtensions.cs
```

### New Models in Iaet.Core

```csharp
// BLE domain models
BleService          — UUID, name, characteristics list, source file, confidence
BleCharacteristic   — UUID, name, operations list, data format, source file
BleOperation        — type (Read/Write/Notify/...), characteristic UUID, data description
BleDataFlow         — characteristic → callback → parser → variable → UI element

// APK analysis models
ApkInfo             — package name, version, min/target SDK, permissions
NetworkSecurityConfig — pinned domains, cleartext-permitted domains
```

---

## Project Structure for Android

```
.iaet-projects/my-app/
  project.json                # targetType: "android"
  apk/
    app.apk                   # original APK
    decompiled/               # jadx output (Java source tree)
    resources/                # apktool output (manifest, strings.xml, etc.)
    mapping.txt               # ProGuard mapping (if available)
  captures/                   # network captures (.iaet.json.gz)
  bluetooth/
    hci-snoop.log             # imported HCI snoop log (optional)
    gatt-services.json        # discovered BLE services
  knowledge/
    endpoints.json            # API endpoints (from source + network)
    bluetooth.json            # BLE services, characteristics, operations, data flows
    permissions.json          # Android permissions analysis
    network-security.json     # cert pinning, cleartext policy
    dependencies.json
  output/
    diagrams/
    api.yaml
    dashboard.html
```

---

## Decompilation

### jadx (primary)

The default decompiler. Produces readable Java source from DEX bytecode.

```bash
iaet apk decompile --project my-app --apk path/to/app.apk
```

This:
1. Copies the APK to `{project}/apk/app.apk`
2. Runs `jadx -d {project}/apk/decompiled/ {project}/apk/app.apk`
3. Stores decompilation metadata (jadx version, duration, file count)

**Requirements:** `jadx` must be on PATH or specified via `--jadx-path`.

### apktool (on demand)

Used when the analyzer needs resources that jadx doesn't extract well:
- `AndroidManifest.xml` (decoded, not binary)
- `res/xml/network_security_config.xml`
- `res/values/strings.xml` (useful for finding API URLs stored as string resources)

Triggered automatically by `ManifestAnalyzer` and `NetworkSecurityAnalyzer` if `resources/` directory doesn't exist.

```bash
apktool d -o {project}/apk/resources/ -f {project}/apk/app.apk
```

### ProGuard mapping

If `mapping.txt` is found in the APK or provided via `--mapping`, it's applied before analysis to recover original class/method names. This dramatically improves analysis quality.

---

## Static Extractors

### ApkUrlExtractor

Scans Java source files for API endpoint patterns. Obfuscation-aware — focuses on string literals and SDK method signatures.

**Patterns matched:**
- String literals: `"https://api.example.com/v1/users"`, `"/api/data"`
- Retrofit annotations: `@GET("users/{id}")`, `@POST("messages")`, `@Headers("Authorization: ...")`
- OkHttp: `Request.Builder().url(...)`, `HttpUrl.parse(...)`
- Volley: `StringRequest(Request.Method.POST, url, ...)`
- URL class: `new URL("...")`, `URI.create("...")`

**Output:** `IReadOnlyList<ExtractedUrl>` — same model as JS URL extractor, with `SourceFile` pointing to the Java file.

### ApkAuthExtractor

Finds authentication patterns and API keys.

**Patterns matched:**
- Constant declarations: `static final String API_KEY = "..."`, `CLIENT_ID`, `CLIENT_SECRET`
- Header construction: `.addHeader("Authorization", "Bearer " + token)`
- OAuth config: `AuthorizationServiceConfiguration`, `client_id`, `redirect_uri`
- SharedPreferences keys: `getString("auth_token", ...)`

**Security:** Values of API keys are stored in `.env.iaet` via secrets store, never in knowledge base or reports.

### BleServiceExtractor

Discovers BLE GATT services from source code.

**Patterns matched (obfuscation-resistant):**
- `UUID.fromString("0000180d-0000-1000-8000-00805f9b34fb")` — string UUID literals always survive obfuscation
- `BluetoothGattService` constructor/usage — match by type
- `BluetoothGatt.getService(uuid)` — match by SDK method
- `ScanFilter.Builder().setServiceUuid(...)` — BLE scan filters

**UUID resolution:** Cross-references discovered UUIDs against Bluetooth SIG standard names table (~200 entries). Reports both raw UUID and human-readable name when available.

### BleOperationExtractor

Discovers what operations the app performs on BLE characteristics.

**Patterns matched:**
- `BluetoothGatt.readCharacteristic(characteristic)`
- `BluetoothGatt.writeCharacteristic(characteristic)`
- `BluetoothGattCharacteristic.setValue(byte[])` → Write
- `BluetoothGatt.setCharacteristicNotification(characteristic, true)` → Subscribe
- `BluetoothGattDescriptor` writes (CCCD enable notification/indication)

**Associates operations with characteristics** by tracing variable assignments: the `characteristic` variable in a `writeCharacteristic()` call traces back to a `getCharacteristic(uuid)` call.

### BleDataFlowTracer

Traces data from BLE characteristic callbacks through parsing to UI. Heuristic-based, designed for obfuscated code.

**Stage 1 — Callback detection:**
Find `onCharacteristicChanged(BluetoothGatt, BluetoothGattCharacteristic)` implementations. The `BluetoothGattCharacteristic` parameter type is never obfuscated.

**Stage 2 — Data parsing:**
In the same class/method, look for:
- `characteristic.getValue()` → `byte[]`
- `ByteBuffer.wrap(value)`, `.getInt()`, `.getFloat()`, `.getShort()`
- Array indexing: `value[0]`, `value[1] & 0xFF`
- Bit manipulation: `(value[0] << 8) | value[1]`

**Stage 3 — Value propagation:**
Track where parsed values go:
- Field assignment: `this.fieldName = parsedValue`
- LiveData/Flow: `liveData.postValue(parsedValue)`, `flow.emit(parsedValue)`
- Intent broadcast: `intent.putExtra("key", parsedValue)`

**Stage 4 — UI binding:**
Search for the field/LiveData name in:
- Layout XML: `@{viewModel.fieldName}` (data binding)
- Java/Kotlin: `textView.setText(String.valueOf(fieldName))`
- Resource IDs: `R.id.temperature_display` (correlated by proximity)

**Output:** `BleDataFlow` records with confidence levels. High confidence when the full chain is traced; low when gaps exist (common in obfuscated code).

**Limitation note:** This is heuristic analysis, not full symbolic execution. It catches common Android patterns (ViewModel + data binding, direct setText, LiveData observers) but may miss custom architectures. Flagged as limitations in the report.

### ManifestAnalyzer

Parses decoded `AndroidManifest.xml` from apktool output.

**Extracts:**
- Package name, version code/name, min/target SDK
- Permissions: `INTERNET`, `BLUETOOTH`, `BLUETOOTH_CONNECT`, `ACCESS_FINE_LOCATION`, etc.
- Exported services, receivers, content providers (attack surface)
- Intent filters (what the app responds to)

### NetworkSecurityAnalyzer

Parses `res/xml/network_security_config.xml`.

**Extracts:**
- Certificate pinning: which domains have pinned certificates
- Cleartext traffic: which domains allow HTTP (not HTTPS)
- Trust anchors: custom CAs

---

## BLE Knowledge Model

### Mapping to IAET concepts

| REST Concept | BLE Equivalent |
|-------------|---------------|
| Host/Base URL | BLE Service UUID |
| Endpoint path | Characteristic UUID |
| HTTP Method | GATT Operation (Read/Write/Notify) |
| Request body | Write payload (byte[]) |
| Response body | Read/Notify payload (byte[]) |
| API documentation | BLE SIG standard names + data flow trace |

### bluetooth.json Schema

```json
{
  "services": [
    {
      "uuid": "0000180d-0000-1000-8000-00805f9b34fb",
      "name": "Heart Rate",
      "standardName": true,
      "sourceFile": "com/example/ble/HeartRateService.java",
      "confidence": "high",
      "characteristics": [
        {
          "uuid": "00002a37-0000-1000-8000-00805f9b34fb",
          "name": "Heart Rate Measurement",
          "operations": ["Notify"],
          "dataFlow": {
            "callback": "onCharacteristicChanged at HeartRateService.java:87",
            "parsing": "ByteBuffer.getShort() → heartRate (int)",
            "uiBinding": "R.id.heart_rate_display via data binding",
            "inferredMeaning": "Heart rate in BPM, displayed on dashboard",
            "confidence": "high"
          }
        }
      ]
    }
  ],
  "hciCorrelation": {
    "imported": false,
    "observedServices": [],
    "notes": "No HCI log imported. Analysis based on static code only."
  }
}
```

---

## HCI Snoop Log Import

### Format

Android's `btsnoop_hci.log` follows the BTSnoop file format:
- 16-byte file header: magic ("btsnoop\0"), version (1), datalink type (HCI)
- Packet records: original length, included length, flags, drops, timestamp, HCI data

### What we extract

From HCI packets, parse:
- **ATT (Attribute Protocol)** packets → GATT read/write/notify operations
- **Service Discovery** → services and characteristics the device actually exposes
- **Connection events** → device addresses, connection parameters

### Correlation

Cross-reference HCI-observed services/characteristics with statically-extracted ones:
- Found in code AND in HCI → `confidence: high`
- Found in code only → `confidence: medium` (may be conditional)
- Found in HCI only → `confidence: high` but `sources: ["hci-log"]` (discovered at runtime, not in code)

---

## Bluetooth SIG Standard UUID Lookup

Embed the ~200 standard Bluetooth GATT service and characteristic UUIDs as a lookup table:

| Short UUID | Full UUID | Name |
|-----------|-----------|------|
| 0x180D | 0000180d-0000-1000-8000-00805f9b34fb | Heart Rate |
| 0x180F | 0000180f-0000-1000-8000-00805f9b34fb | Battery Service |
| 0x2A37 | 00002a37-0000-1000-8000-00805f9b34fb | Heart Rate Measurement |
| 0x2A19 | 00002a19-0000-1000-8000-00805f9b34fb | Battery Level |
| ... | ... | ... |

Custom UUIDs (not in the SIG table) are reported as "Custom Service" / "Custom Characteristic" with the raw UUID.

---

## CLI Commands

```
iaet apk decompile --project <name> --apk <path>
                   [--jadx-path <path>] [--mapping <path>]

iaet apk analyze --project <name>
                 [--skip-ble] [--skip-network]

iaet apk ble --project <name>
             [--hci-log <path>]
             [--trace-dataflow]
```

### iaet apk decompile

1. Copies APK to project directory
2. Runs jadx → `apk/decompiled/`
3. If manifest/network analysis needed, runs apktool → `apk/resources/`
4. Applies ProGuard mapping if provided
5. Reports: file count, package structure, estimated obfuscation level

### iaet apk analyze

Runs all extractors on decompiled source:
1. URL extraction → `knowledge/endpoints.json`
2. Auth extraction → secrets in `.env.iaet`, patterns in knowledge
3. Manifest analysis → `knowledge/permissions.json`
4. Network security → `knowledge/network-security.json`
5. BLE services → `knowledge/bluetooth.json`

### iaet apk ble

Focused BLE analysis:
1. Service/characteristic extraction
2. Operation discovery
3. Data flow tracing (if `--trace-dataflow`)
4. HCI log import and correlation (if `--hci-log`)

---

## Agent Integration

### New agent: agents/apk-analyzer.md

A specialist agent that:
1. Runs `iaet apk decompile` and `iaet apk analyze`
2. Reviews extracted endpoints and BLE services
3. Cross-references with network captures (if any)
4. Feeds findings into the investigation knowledge base
5. Flags items needing human review (low-confidence traces, obfuscated paths)

The Lead Investigator dispatches this agent when `project.json` has `targetType: "android"`.

---

## Dashboard Integration

The dashboard gains:
- **Bluetooth tab** — BLE services, characteristics, operations, data flow traces
- **Permissions panel** — Android permissions with risk assessment
- **Network security panel** — cert pinning status, cleartext domains

BLE services render as expandable cards similar to API endpoints, with characteristics nested inside.

---

## Implementation Phases

**Phase 1: Decompilation + URL/Auth extraction**
- `JadxRunner`, `ApktoolRunner`
- `ApkUrlExtractor`, `ApkAuthExtractor`
- `ManifestAnalyzer`, `NetworkSecurityAnalyzer`
- CLI commands, project structure
- Agent prompt

**Phase 2: BLE service discovery**
- `BleServiceExtractor`, `BleOperationExtractor`
- `BleSigLookup` (standard UUID table)
- `bluetooth.json` knowledge model
- Dashboard Bluetooth tab

**Phase 3: BLE data flow tracing**
- `BleDataFlowTracer` (heuristic, obfuscation-aware)
- Callback → parser → variable → UI chain
- Confidence annotations

**Phase 4: HCI log import**
- `HciLogImporter` (btsnoop binary format parser)
- ATT packet extraction
- Static ↔ runtime correlation

---

## Dependency Map

```
Iaet.Core (existing)
    │
    ├── Iaet.Android (new)
    │   ├── depends on: Iaet.Core, Iaet.JsAnalysis (reuse URL/config extractors)
    │   └── optional runtime: jadx, apktool on PATH
    │
    ├── Iaet.Diagrams (existing) — BLE service topology diagrams
    ├── Iaet.Export (existing) — BLE in reports and OpenAPI
    └── Iaet.Cli (existing) — new apk commands
```
