# Iaet.Native

Native binary analysis for IAET investigation projects: ELF/PE/Mach-O identification, ELF dynamic
symbol tables, string and artifact extraction (URLs, BLE UUIDs, JNI bindings), library fingerprinting,
and an optional Ghidra headless provider that exports functions, call graphs and pseudocode.

Every finding carries `Evidence` (provider, artifact hash, location, confidence, limitations) and
open questions are recorded as `ResidualUnknown` entries rather than dropped.
