# IAET Phase 7: Native Binary Analysis, Evidence Contract, and `iaet doctor`

**Goal:** Transfer the patterns that make REA ("Reverse Engineer Anything") and Ghidra effective for agent-driven reverse engineering into IAET, so an investigation can follow a feature from the UI, through decompiled Java, into native code — and so every finding states how it was established.

**Sources reviewed:**
- Cyber Security News, *REA Tool Connects Claude Code and Cursor to Ghidra and IDA Pro to Reverse Engineer Anything* (Oct 2026) → project `morluto/rea`, MIT, Node/TypeScript CLI + MCP server
- `NationalSecurityAgency/ghidra` — the SRE framework REA (and now IAET) delegates native analysis to

**Branch:** `ccr-333e8de5-twoyss`

---

## 1. What the sources do and what is worth borrowing

### REA — "reverse engineer anything with agents"

REA is a bridge, not a disassembler. It gives a coding agent (Claude Code, Cursor, Codex, ...) one CLI and one MCP server whose tools inspect a target and return **evidence**: pseudocode, assembly, strings, symbols, call graphs and cross-references for native binaries; modules, imports, source maps, IPC and routes for JavaScript/Electron; metadata and CIL for .NET; manifests and decompiled methods for APKs; HAR/mitmdump network captures; PTY process captures. Native work is delegated to a **provider** (Hopper, Ghidra via a packaged headless Java script, IDA via `ida-pro-mcp`, JEB) selected deterministically by a capability router. The agent then explains the behavior, writes an implementation and tests it.

Design principles REA states explicitly, and how they map to IAET:

| REA principle | What it means | IAET today | Transferred as |
|---|---|---|---|
| **Evidence over assertion** — "distinguish observations, derivations, inferences, and unknowns"; every result carries limitations; incomplete evidence never implies equivalence | Findings are records with provenance, confidence and what they do *not* prove | `DiscoveredEndpoint` has `Confidence`, `Sources`, `Limitations`; agent prompts say "annotate confidence" | `Evidence` + `EvidenceKind` model, `ResidualUnknown` ledger (`knowledge/unknowns.json`), evidence-discipline section in agent prompts |
| **Provider neutrality / capability router** — providers declare capabilities and side effects; `rea doctor`, `rea providers`, `rea capabilities` | Tools are probed before an investigation depends on them | jadx checked ad hoc inside `apk decompile`; Playwright, Node, Java unchecked | `IToolProvider` abstraction + `iaet doctor [--json]` |
| **Agent-first bridge to a real decompiler** — packaged Ghidra headless script exports inventory, decompiler output, references and CFG | Ghidra does the hard work; the tool shapes its output for an agent | No native-code path at all (`TargetType.Desktop` exists but is unused; user guide lists ELF/PE as "Planned") | `Iaet.Native` with `GhidraHeadlessRunner` + embedded `IaetGhidraExport.java` post-script |
| **Snapshot cache** — exact match on target bytes, operation, parameters and provider reuses the saved result | Expensive provider runs are never repeated for identical inputs | None | `NativeAnalysisCache` (SHA-256 of binary + options + script version) under `<project>/native/work/cache/` |
| **Bounded, question-shaped tools** — `function` dossier, `search`, `trace`, `xrefs`, with size budgets and truncation flags | Return what an analyst asks for, inline, with limits stated | Knowledge files are dumped whole into prompts | `iaet native function` (dossier), `iaet native search` (bounded), `--max-functions` / `maxPseudocodeChars` truncation reported as limitations |
| **Local by design, bring-your-own engine, read-only analysis, consent-based setup** | Never download engines; never mutate the target; show planned changes | Already the IAET stance (secrets, redaction) | Ghidra is located via env vars/PATH, never installed; `-deleteProject` scratch DB; input file never modified |
| **One workflow, two interfaces (CLI + MCP)** | The same workflows and contracts behind both | CLI only; agents call it through bash | Documented as follow-up (§5) — `iaet mcp` |

### Ghidra — what IAET takes from the framework itself

| Ghidra concept | Transferred as |
|---|---|
| **Headless analyzer** (`support/analyzeHeadless <proj> <name> -import <bin> -postScript <Script.java> <args> -deleteProject -analysisTimeoutPerFile N`) with pre/post scripts that receive `getScriptArgs()` and `currentProgram` | `GhidraHeadlessRunner.BuildArguments` + `IaetGhidraExport.java` (uses `FunctionManager`, `ReferenceManager`, `DecompInterface`, `StringDataInstance`, `MemoryBlock`) |
| **Program** = one imported binary with language id, compiler spec, image base and executable SHA-256 | `program` block of the export; `NativeBinaryInfo.Sha256`; evidence keyed on it |
| **Function ID / library identification** (recognize known library code so analysts skip boilerplate) | `LibraryFingerprinter` — string/symbol signatures for OpenSSL, BoringSSL, mbedTLS, libcurl, SQLite, zlib, protobuf, gRPC, Flutter, React Native, Unity, OpenCV, FFmpeg, WebRTC, Rust/Go runtimes, Qt, nRF SDK, ESP-IDF, ... |
| **Decompiler output and call graph** (`getCallingFunctions`/`getCalledFunctions`, decompiled C) | `NativeFunction { Callers, Callees, ReferencedStrings, Pseudocode }` |
| **Loaders for many formats** | `BinaryFormatDetector` (ELF/PE/Mach-O/fat) + `ElfReader` (.dynsym/.dynstr/.dynamic) run without Ghidra; Ghidra's loaders take over when present |
| **Version tracking** (diff two program versions) | Follow-up (§5): diff two `native-binaries.json` / APK versions |
| **P-Code / SLEIGH** | Not transferred — IAET consumes decompiler output, it does not reimplement analysis |

### The IAET-specific insight: JNI is the seam

jadx shows `native` method declarations in Java; the `.so` exports `Java_<pkg>_<Class>_<method>` symbols. Decoding those names (`JniNameDecoder`, with `_1`/`_00024`/`__sig` mangling rules) links every native entry point back to the Java class the UI calls. That is the equivalent of REA's `trace_feature` for Android: UI → Java → JNI binding → native function → callees → byte layout. `output/diagrams/jni-bridge.mmd` draws it.

---

## 2. Scope delivered in this phase

| Area | File(s) | Status |
|---|---|---|
| Evidence contract | `src/Iaet.Core/Models/Evidence.cs`, `EvidenceKind.cs`, `ResidualUnknown.cs` | Done |
| Provider abstraction | `src/Iaet.Core/Abstractions/IToolProvider.cs`, `Models/ToolHealth.cs` | Done |
| Native models | `src/Iaet.Core/Models/NativeBinaryInfo.cs` (`NativeSymbol`, `JniBinding`, `LibraryFingerprint`, `NativeFunction`) | Done |
| Format detection + ELF | `src/Iaet.Native/BinaryFormatDetector.cs`, `Elf/ElfReader.cs` | Done (ELF32/64 LE+BE; PE/Mach-O header-level only) |
| Strings + artifacts | `Extractors/BinaryStringExtractor.cs`, `NativeArtifactExtractor.cs` (URLs, BLE UUIDs, JNI), `JniNameDecoder.cs`, `LibraryFingerprinter.cs` | Done |
| Ghidra provider | `Ghidra/GhidraLocator.cs`, `GhidraHeadlessRunner.cs`, `IaetGhidraExport.java` (embedded), `GhidraExportParser.cs`, `GhidraToolProvider.cs` | Done; runner/arguments/parser unit-tested, Java script not executed against a live Ghidra in CI |
| Snapshot cache | `src/Iaet.Native/NativeAnalysisCache.cs` | Done |
| Orchestration | `src/Iaet.Native/NativeBinaryAnalyzer.cs` | Done |
| CLI | `iaet native analyze|search|function`, `iaet doctor [--json]`; `iaet apk analyze` now points at native libs | Done |
| Agents | `agents/native-analyzer.md`; Lead Investigator APK path, evidence rule, unknowns ledger; APK Analyzer hand-off | Done |
| Docs | README, AGENTS.md, architecture.md, user-guide.md, this plan | Done |
| Tests | `tests/Iaet.Native.Tests` (synthetic ELF image builder, 55 tests), CLI tests for `native`/`doctor` | Done |
| Hygiene | `SQLitePCLRaw.bundle_e_sqlite3` pinned to 2.1.12 so `dotnet restore` passes NuGet audit (GHSA-2m69-gcr7-jv3q) | Done |

---

## 3. Knowledge files

### `knowledge/native-binaries.json`
```json
{
  "analyzedAt": "2026-10-11T01:08:00Z",
  "binaries": [{
    "path": "apk/decompiled/resources/lib/arm64-v8a/libbridge.so",
    "sha256": "…", "format": "elf", "architecture": "aarch64", "bitness": 64, "abi": "arm64-v8a",
    "neededLibraries": ["libssl.so", "liblog.so"],
    "imports": [{ "name": "SSL_CTX_new", "type": "function" }],
    "exports": [{ "name": "Java_com_vendor_radio_NativeBridge_setFrequency", "type": "function", "address": 4096 }],
    "jniBindings": [{ "symbol": "Java_com_vendor_radio_NativeBridge_setFrequency", "javaClass": "com.vendor.radio.NativeBridge", "javaMethod": "setFrequency" }],
    "urls": [{ "url": "https://api.vendor-device.com/v2/devices/%s/status", "confidence": "medium", "context": "format string — runtime substitution required" }],
    "bleUuids": ["0000FFF0-0000-1000-8000-00805F9B34FB"],
    "libraries": [{ "library": "OpenSSL", "version": "1.1.1w", "confidence": "high", "matchedIndicators": ["string:OpenSSL", "symbol:SSL_*"] }],
    "functions": [{ "name": "encode_frame", "entryAddress": "0x101100", "callers": ["Java_…_setFrequency"], "callees": [], "referencedStrings": ["CMD:%02x"], "pseudocode": "…" }],
    "providers": ["format-detector", "elf-reader", "strings", "ghidra"],
    "evidence": [{ "kind": "observation", "provider": "elf-reader", "artifact": "…", "artifactSha256": "…", "location": ".dynsym/.dynamic", "confidence": "high", "limitations": [] }],
    "unknowns": [], "limitations": ["Pseudocode not exported — re-run with --decompile to include it."]
  }]
}
```

### `knowledge/unknowns.json`
```json
{
  "updatedAt": "…", "open": 1,
  "unknowns": [{
    "id": "ghidra-missing-libbridge.so",
    "question": "What does the native code in libbridge.so actually do? (no decompiler ran)",
    "source": "ghidra", "context": "apk/…/libbridge.so",
    "suggestedAction": "Install Ghidra, set GHIDRA_INSTALL_DIR, and re-run `iaet native analyze --project <name> --decompile`.",
    "status": "open", "raisedAt": "…"
  }]
}
```
Entries are merged by `id`; a human or the Lead can flip `status` to `resolved`/`wontFix` and the analyzer will not re-add them.

---

## 4. Verification performed

- `dotnet build Iaet.slnx -c Release` — clean with `TreatWarningsAsErrors` + `AnalysisMode=All`
- `dotnet test Iaet.slnx` — all projects green (Native: 55 tests; CLI: +10)
- End-to-end smoke: compiled a JNI-style shared library with gcc containing two `Java_*` exports, URL literals, BLE UUIDs and an OpenSSL banner; `iaet native analyze --binary` produced the expected `native-binaries.json` (2 JNI bindings decoded incl. `__3B` overload signature, URL confidences High/Medium, base UUID detected, OpenSSL 1.1.1w fingerprinted), `unknowns.json` (Ghidra-missing entry) and `jni-bridge.mmd`; `iaet native search --query frequency` returned export + jni hits; `iaet doctor` reported dotnet/java/node/playwright present and jadx/apktool/ghidra missing with install hints
- **Not verified here:** a live `analyzeHeadless` run (Ghidra is not installed in the build container). The Java script follows the public GhidraScript API (`DecompInterface`, `FunctionManager`, `ReferenceManager`, `StringDataInstance`) and is unit-tested only for argument construction, materialization and export parsing. First real run should be against Ghidra 11.x with a small `.so`.

---

## 5. Follow-ups (not in this phase)

1. **`iaet mcp`** — expose the CLI workflows as MCP tools (REA's "one workflow, two interfaces") using the official `ModelContextProtocol` .NET SDK, so Claude Code/Cursor call `native_function`, `endpoints_search`, `knowledge_read` directly instead of via bash. Return the same `Evidence` envelope.
2. **Version tracking / compare** — `iaet analyze diff --before <knowledge snapshot> --after` for two APK versions (Ghidra Version Tracking, REA `compare`), highlighting new endpoints, changed JNI surfaces, changed BLE UUIDs.
3. **PE/Mach-O import tables** — static parsing for Windows/macOS desktop targets without Ghidra (`TargetType.Desktop`).
4. **Trace command** — `iaet native trace --query <text>`: strings → xrefs → functions → callers chain in one bounded result, mirroring REA `trace`.
5. **Electron / ASAR and .NET assemblies** — REA's other static targets; IAET's JS analyzer already covers bundles, so ASAR extraction + `System.Reflection.Metadata` reading are the gaps.
6. **Evidence everywhere** — attach `Evidence` to `DiscoveredEndpoint`, BLE and protocol knowledge produced by the existing analyzers, and surface `unknowns.json` in the dashboard's Next Steps tab.
