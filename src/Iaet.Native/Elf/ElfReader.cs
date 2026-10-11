using System.Buffers.Binary;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Native.Elf;

/// <summary>Result of parsing an ELF image's headers and dynamic symbol table.</summary>
public sealed record ElfInfo
{
    public required int Bitness { get; init; }
    public required bool LittleEndian { get; init; }
    public required string Architecture { get; init; }
    public required string Type { get; init; }
    public IReadOnlyList<ElfSection> Sections { get; init; } = [];
    public IReadOnlyList<string> NeededLibraries { get; init; } = [];
    public string? SoName { get; init; }
    public IReadOnlyList<NativeSymbol> Imports { get; init; } = [];
    public IReadOnlyList<NativeSymbol> Exports { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record ElfSection(string Name, uint Type, ulong Offset, ulong Size, ulong Address);

/// <summary>
/// Minimal, dependency-free ELF32/ELF64 reader. It reads just enough to answer the questions an
/// API investigation asks of an Android <c>.so</c>: what architecture, which libraries it links,
/// which symbols it imports (libc, OpenSSL, JNI) and which it exports (JNI entry points).
/// Full disassembly and decompilation are delegated to the Ghidra provider.
/// </summary>
public static class ElfReader
{
    private const uint ShtDynsym = 11;
    private const uint ShtStrtab = 3;
    private const uint ShtDynamic = 6;
    private const int DtNeeded = 1;
    private const int DtSoName = 14;
    private const int DtStrTab = 5;

    public static bool IsElf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[0] == 0x7F && bytes[1] == (byte)'E' && bytes[2] == (byte)'L' && bytes[3] == (byte)'F';

    public static ElfInfo Parse(ReadOnlySpan<byte> bytes)
    {
        if (!IsElf(bytes) || bytes.Length < 52)
            throw new InvalidDataException("Not an ELF image.");

        try
        {
            return ParseCore(bytes);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or OverflowException)
        {
            // Packed, truncated or hand-crafted images can carry header fields that point past the file.
            throw new InvalidDataException($"Malformed ELF image: {ex.Message}", ex);
        }
    }

    private static ElfInfo ParseCore(ReadOnlySpan<byte> bytes)
    {

        var is64 = bytes[4] == 2;
        var le = bytes[5] != 2;
        var r = new Reader(le);
        var warnings = new List<string>();

        if (is64 && bytes.Length < 64)
            throw new InvalidDataException("Truncated ELF64 header.");

        var type = r.U16(bytes, 16);
        var machine = r.U16(bytes, 18);

        ulong shoff;
        int shentsize, shnum, shstrndx;
        if (is64)
        {
            shoff = r.U64(bytes, 40);
            shentsize = r.U16(bytes, 58);
            shnum = r.U16(bytes, 60);
            shstrndx = r.U16(bytes, 62);
        }
        else
        {
            shoff = r.U32(bytes, 32);
            shentsize = r.U16(bytes, 46);
            shnum = r.U16(bytes, 48);
            shstrndx = r.U16(bytes, 50);
        }

        var sections = new List<(string Name, uint Type, ulong Offset, ulong Size, ulong Addr, uint Link, ulong EntSize, uint NameOff)>();
        if (shoff == 0 || shnum == 0 || shoff + (ulong)(shentsize * shnum) > (ulong)bytes.Length)
        {
            warnings.Add("Section header table missing or truncated; symbol tables unavailable (stripped or packed binary).");
        }
        else
        {
            for (var i = 0; i < shnum; i++)
            {
                var off = (int)shoff + i * shentsize;
                var nameOff = r.U32(bytes, off);
                var shType = r.U32(bytes, off + 4);
                ulong shOffset, shSize, shAddr, entSize;
                uint link;
                if (is64)
                {
                    shAddr = r.U64(bytes, off + 16);
                    shOffset = r.U64(bytes, off + 24);
                    shSize = r.U64(bytes, off + 32);
                    link = r.U32(bytes, off + 40);
                    entSize = r.U64(bytes, off + 56);
                }
                else
                {
                    shAddr = r.U32(bytes, off + 12);
                    shOffset = r.U32(bytes, off + 16);
                    shSize = r.U32(bytes, off + 20);
                    link = r.U32(bytes, off + 24);
                    entSize = r.U32(bytes, off + 36);
                }
                sections.Add((string.Empty, shType, shOffset, shSize, shAddr, link, entSize, nameOff));
            }

            // Resolve section names through the section-name string table.
            if (shstrndx < sections.Count)
            {
                var strtab = sections[shstrndx];
                for (var i = 0; i < sections.Count; i++)
                {
                    var s = sections[i];
                    sections[i] = s with { Name = ReadCString(bytes, strtab.Offset, strtab.Size, s.NameOff) };
                }
            }
        }

        var imports = new List<NativeSymbol>();
        var exports = new List<NativeSymbol>();
        var needed = new List<string>();
        string? soName = null;

        foreach (var sec in sections.Where(s => s.Type == ShtDynsym))
        {
            if (sec.Link >= sections.Count || sections[(int)sec.Link].Type != ShtStrtab)
            {
                warnings.Add($"Dynamic symbol table '{sec.Name}' has no linked string table.");
                continue;
            }
            var strtab = sections[(int)sec.Link];
            var entSize = sec.EntSize == 0 ? (ulong)(is64 ? 24 : 16) : sec.EntSize;
            var count = (int)(sec.Size / entSize);
            if (sec.Offset + sec.Size > (ulong)bytes.Length)
            {
                warnings.Add($"Dynamic symbol table '{sec.Name}' extends past end of file.");
                continue;
            }

            for (var i = 1; i < count; i++) // index 0 is the null symbol
            {
                var off = (int)sec.Offset + i * (int)entSize;
                uint nameOff; byte info; ushort shndx; ulong value;
                if (is64)
                {
                    nameOff = r.U32(bytes, off);
                    info = bytes[off + 4];
                    shndx = r.U16(bytes, off + 6);
                    value = r.U64(bytes, off + 8);
                }
                else
                {
                    nameOff = r.U32(bytes, off);
                    value = r.U32(bytes, off + 4);
                    info = bytes[off + 12];
                    shndx = r.U16(bytes, off + 14);
                }

                var name = ReadCString(bytes, strtab.Offset, strtab.Size, nameOff);
                if (string.IsNullOrEmpty(name))
                    continue;

                var symType = (info & 0xF) switch { 1 => "object", 2 => "function", _ => "unknown" };
                var binding = info >> 4; // 0 local, 1 global, 2 weak
                if (binding == 0)
                    continue;

                if (shndx == 0)
                    imports.Add(new NativeSymbol { Name = name, Type = symType });
                else
                    exports.Add(new NativeSymbol { Name = name, Type = symType, Address = value });
            }
        }

        foreach (var dyn in sections.Where(s => s.Type == ShtDynamic))
        {
            var entSize = is64 ? 16 : 8;
            var count = (int)(dyn.Size / (ulong)entSize);
            if (dyn.Offset + dyn.Size > (ulong)bytes.Length)
                continue;

            // DT_STRTAB holds a virtual address; map it back to the file through the section that contains it.
            ulong strtabAddr = 0;
            for (var i = 0; i < count; i++)
            {
                var off = (int)dyn.Offset + i * entSize;
                var tag = is64 ? (long)r.U64(bytes, off) : r.U32(bytes, off);
                if (tag == DtStrTab)
                    strtabAddr = is64 ? r.U64(bytes, off + 8) : r.U32(bytes, off + 4);
            }

            var dynstr = sections.FirstOrDefault(s => s.Type == ShtStrtab && s.Addr == strtabAddr && strtabAddr != 0);
            if (dynstr.Size == 0)
                dynstr = sections.FirstOrDefault(s => s.Type == ShtStrtab && s.Name == ".dynstr");
            if (dynstr.Size == 0)
                continue;

            for (var i = 0; i < count; i++)
            {
                var off = (int)dyn.Offset + i * entSize;
                var tag = is64 ? (long)r.U64(bytes, off) : r.U32(bytes, off);
                var val = is64 ? r.U64(bytes, off + 8) : r.U32(bytes, off + 4);
                if (tag == 0)
                    break;
                if (tag == DtNeeded)
                    needed.Add(ReadCString(bytes, dynstr.Offset, dynstr.Size, (uint)val));
                else if (tag == DtSoName)
                    soName = ReadCString(bytes, dynstr.Offset, dynstr.Size, (uint)val);
            }
        }

        return new ElfInfo
        {
            Bitness = is64 ? 64 : 32,
            LittleEndian = le,
            Architecture = BinaryFormatDetector.ElfMachineName(machine),
            Type = type switch { 1 => "relocatable", 2 => "executable", 3 => "shared-object", 4 => "core", _ => $"type-{type}" },
            Sections = sections.Select(s => new ElfSection(s.Name, s.Type, s.Offset, s.Size, s.Addr)).ToList(),
            NeededLibraries = needed,
            SoName = soName,
            Imports = imports,
            Exports = exports,
            Warnings = warnings,
        };
    }

    private static string ReadCString(ReadOnlySpan<byte> bytes, ulong tableOffset, ulong tableSize, uint nameOffset)
    {
        if (nameOffset >= tableSize)
            return string.Empty;
        var start = tableOffset + nameOffset;
        if (start >= (ulong)bytes.Length)
            return string.Empty;
        var max = (int)Math.Min(tableSize - nameOffset, (ulong)bytes.Length - start);
        var slice = bytes.Slice((int)start, max);
        var end = slice.IndexOf((byte)0);
        if (end < 0)
            end = slice.Length;
        return Encoding.ASCII.GetString(slice[..end]);
    }

    private readonly struct Reader(bool littleEndian)
    {
        public ushort U16(ReadOnlySpan<byte> b, int off) =>
            littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(b[off..]) : BinaryPrimitives.ReadUInt16BigEndian(b[off..]);

        public uint U32(ReadOnlySpan<byte> b, int off) =>
            littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(b[off..]) : BinaryPrimitives.ReadUInt32BigEndian(b[off..]);

        public ulong U64(ReadOnlySpan<byte> b, int off) =>
            littleEndian ? BinaryPrimitives.ReadUInt64LittleEndian(b[off..]) : BinaryPrimitives.ReadUInt64BigEndian(b[off..]);
    }
}
