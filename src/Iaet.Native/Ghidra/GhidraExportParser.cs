using System.Text.Json;
using Iaet.Core.Models;

namespace Iaet.Native.Ghidra;

public sealed record GhidraExport
{
    public string ScriptVersion { get; init; } = string.Empty;
    public string ProgramName { get; init; } = string.Empty;
    public string ExecutableFormat { get; init; } = string.Empty;
    public string LanguageId { get; init; } = string.Empty;
    public string ImageBase { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public IReadOnlyList<NativeFunction> Functions { get; init; } = [];
    public IReadOnlyList<(string Address, string Value)> Strings { get; init; } = [];
    public IReadOnlyList<NativeSymbol> Externals { get; init; } = [];
    public int FunctionCount { get; init; }
    public bool Truncated { get; init; }
}

/// <summary>Reads the JSON written by <c>IaetGhidraExport.java</c> into IAET models.</summary>
public static class GhidraExportParser
{
    public static GhidraExport Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Parse(doc.RootElement);
    }

    public static GhidraExport Parse(JsonElement root)
    {
        var program = root.TryGetProperty("program", out var p) ? p : default;

        var functions = new List<NativeFunction>();
        if (root.TryGetProperty("functions", out var fns) && fns.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in fns.EnumerateArray())
            {
                functions.Add(new NativeFunction
                {
                    Name = Str(f, "name"),
                    EntryAddress = Str(f, "entry"),
                    Size = f.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var size) ? size : 0,
                    IsThunk = Bool(f, "isThunk"),
                    IsExternal = Bool(f, "isExternal"),
                    Callers = StrList(f, "callers"),
                    Callees = StrList(f, "callees"),
                    ReferencedStrings = StrList(f, "strings"),
                    Pseudocode = f.TryGetProperty("pseudocode", out var pc) && pc.ValueKind == JsonValueKind.String ? pc.GetString() : null,
                });
            }
        }

        var strings = new List<(string, string)>();
        if (root.TryGetProperty("strings", out var strs) && strs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in strs.EnumerateArray())
                strings.Add((Str(s, "address"), Str(s, "value")));
        }

        var externals = new List<NativeSymbol>();
        if (root.TryGetProperty("externals", out var ext) && ext.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in ext.EnumerateArray())
                externals.Add(new NativeSymbol { Name = Str(e, "name"), Type = Str(e, "type") is "function" ? "function" : "object" });
        }

        return new GhidraExport
        {
            ScriptVersion = Str(root, "scriptVersion"),
            ProgramName = Str(program, "name"),
            ExecutableFormat = Str(program, "executableFormat"),
            LanguageId = Str(program, "languageId"),
            ImageBase = Str(program, "imageBase"),
            Sha256 = Str(program, "sha256"),
            Functions = functions,
            Strings = strings,
            Externals = externals,
            FunctionCount = root.TryGetProperty("functionCount", out var fc) && fc.TryGetInt32(out var count) ? count : functions.Count,
            Truncated = Bool(root, "truncated"),
        };
    }

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static bool Bool(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static List<string> StrList(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return [];
        return v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList();
    }
}
