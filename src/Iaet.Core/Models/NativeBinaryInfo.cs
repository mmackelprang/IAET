namespace Iaet.Core.Models;

public enum BinaryFormat
{
    Unknown,
    Elf,
    Pe,
    MachO,
    MachOFat,
}

/// <summary>A symbol imported from or exported by a native binary.</summary>
public sealed record NativeSymbol
{
    public required string Name { get; init; }

    /// <summary>"function", "object", or "unknown".</summary>
    public string Type { get; init; } = "unknown";

    /// <summary>Address (ELF st_value) when defined; null for imports.</summary>
    public ulong? Address { get; init; }
}

/// <summary>
/// A JNI export decoded from its mangled symbol name: <c>Java_com_example_Foo_bar</c> →
/// class <c>com.example.Foo</c>, method <c>bar</c>. This is the bridge between decompiled Java
/// (jadx) and native code (Ghidra).
/// </summary>
public sealed record JniBinding
{
    public required string Symbol { get; init; }
    public required string JavaClass { get; init; }
    public required string JavaMethod { get; init; }

    /// <summary>Overload signature suffix when present (after <c>__</c>).</summary>
    public string? Signature { get; init; }
}

/// <summary>A third-party library detected inside a native binary by string/symbol fingerprint.</summary>
public sealed record LibraryFingerprint
{
    public required string Library { get; init; }
    public string? Version { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public IReadOnlyList<string> MatchedIndicators { get; init; } = [];
}

/// <summary>A function recovered by a decompiler engine (Ghidra headless export).</summary>
public sealed record NativeFunction
{
    public required string Name { get; init; }
    public required string EntryAddress { get; init; }
    public long Size { get; init; }
    public bool IsThunk { get; init; }
    public bool IsExternal { get; init; }
    public IReadOnlyList<string> Callers { get; init; } = [];
    public IReadOnlyList<string> Callees { get; init; } = [];

    /// <summary>String literals referenced from the function body.</summary>
    public IReadOnlyList<string> ReferencedStrings { get; init; } = [];

    /// <summary>Decompiled pseudocode when decompilation was requested and succeeded.</summary>
    public string? Pseudocode { get; init; }
}

/// <summary>
/// Everything IAET knows about one native binary (an APK <c>lib/*.so</c>, a desktop executable, a firmware blob).
/// </summary>
public sealed record NativeBinaryInfo
{
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
    public long SizeBytes { get; init; }
    public required BinaryFormat Format { get; init; }
    public string Architecture { get; init; } = "unknown";
    public int Bitness { get; init; }
    public bool LittleEndian { get; init; } = true;

    /// <summary>Android ABI folder the library came from (arm64-v8a, armeabi-v7a, x86_64, ...), when applicable.</summary>
    public string? Abi { get; init; }

    public IReadOnlyList<string> NeededLibraries { get; init; } = [];
    public IReadOnlyList<NativeSymbol> Imports { get; init; } = [];
    public IReadOnlyList<NativeSymbol> Exports { get; init; } = [];
    public IReadOnlyList<JniBinding> JniBindings { get; init; } = [];
    public IReadOnlyList<ExtractedUrl> Urls { get; init; } = [];
    public IReadOnlyList<string> BleUuids { get; init; } = [];
    public IReadOnlyList<LibraryFingerprint> Libraries { get; init; } = [];
    public int StringCount { get; init; }

    /// <summary>Functions from the decompiler provider; empty when only static header analysis ran.</summary>
    public IReadOnlyList<NativeFunction> Functions { get; init; } = [];

    /// <summary>Which providers contributed ("elf-reader", "strings", "ghidra").</summary>
    public IReadOnlyList<string> Providers { get; init; } = [];

    public IReadOnlyList<Evidence> Evidence { get; init; } = [];
    public IReadOnlyList<ResidualUnknown> Unknowns { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
}
