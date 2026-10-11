using System.Buffers.Binary;
using System.Text;

namespace Iaet.Native.Tests.Elf;

/// <summary>
/// Builds a minimal but well-formed ELF64 little-endian shared object in memory with a
/// .dynsym/.dynstr pair and a .dynamic section, so the reader can be tested without shipping binaries.
/// </summary>
internal static class ElfTestImage
{
    internal sealed record Sym(string Name, bool Defined, bool IsFunction, ulong Value = 0x1000);

    public static byte[] Build(IReadOnlyList<Sym> symbols, IReadOnlyList<string> needed, string? soName = null, ushort machine = 0xB7, byte[]? extraData = null)
    {
        // String tables
        var dynstr = new List<byte> { 0 };
        var strOffsets = new Dictionary<string, uint>(StringComparer.Ordinal);
        uint AddStr(string s)
        {
            if (strOffsets.TryGetValue(s, out var existing))
                return existing;
            var off = (uint)dynstr.Count;
            dynstr.AddRange(Encoding.ASCII.GetBytes(s));
            dynstr.Add(0);
            strOffsets[s] = off;
            return off;
        }
        foreach (var s in symbols) AddStr(s.Name);
        foreach (var n in needed) AddStr(n);
        if (soName is not null) AddStr(soName);

        var shstr = new List<byte> { 0 };
        uint AddShStr(string s)
        {
            var off = (uint)shstr.Count;
            shstr.AddRange(Encoding.ASCII.GetBytes(s));
            shstr.Add(0);
            return off;
        }
        var nDynsym = AddShStr(".dynsym");
        var nDynstr = AddShStr(".dynstr");
        var nDynamic = AddShStr(".dynamic");
        var nShstrtab = AddShStr(".shstrtab");
        var nData = AddShStr(".rodata");

        // .dynsym: null symbol + entries (24 bytes each)
        var dynsym = new List<byte>(new byte[24]);
        foreach (var s in symbols)
        {
            var e = new byte[24];
            BinaryPrimitives.WriteUInt32LittleEndian(e.AsSpan(0), strOffsets[s.Name]);
            e[4] = (byte)((1 << 4) | (s.IsFunction ? 2 : 1)); // STB_GLOBAL | type
            e[5] = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(6), s.Defined ? (ushort)5 : (ushort)0);
            BinaryPrimitives.WriteUInt64LittleEndian(e.AsSpan(8), s.Defined ? s.Value : 0);
            dynsym.AddRange(e);
        }

        // Layout: ehdr(64) | dynsym | dynstr | dynamic | rodata | shstrtab | shdrs
        const int ehdrSize = 64;
        var dynsymOff = ehdrSize;
        var dynstrOff = dynsymOff + dynsym.Count;
        var dynamicOff = Align(dynstrOff + dynstr.Count, 8);
        var dynstrAddr = (ulong)dynstrOff; // identity mapping for the test image

        var dynamic = new List<byte>();
        void Dyn(long tag, ulong val)
        {
            var e = new byte[16];
            BinaryPrimitives.WriteInt64LittleEndian(e.AsSpan(0), tag);
            BinaryPrimitives.WriteUInt64LittleEndian(e.AsSpan(8), val);
            dynamic.AddRange(e);
        }
        foreach (var n in needed) Dyn(1, strOffsets[n]);
        Dyn(5, dynstrAddr);
        if (soName is not null) Dyn(14, strOffsets[soName]);
        Dyn(0, 0);

        var rodataOff = dynamicOff + dynamic.Count;
        var rodata = extraData ?? [];
        var shstrOff = rodataOff + rodata.Length;
        var shOff = Align(shstrOff + shstr.Count, 8);
        const int shCount = 6; // null, dynsym, dynstr, dynamic, rodata, shstrtab

        var total = shOff + shCount * 64;
        var img = new byte[total];

        // ELF header
        img[0] = 0x7F; img[1] = (byte)'E'; img[2] = (byte)'L'; img[3] = (byte)'F';
        img[4] = 2; // 64-bit
        img[5] = 1; // little endian
        img[6] = 1; // version
        BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(16), 3); // ET_DYN
        BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(18), machine);
        BinaryPrimitives.WriteUInt32LittleEndian(img.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(img.AsSpan(40), (ulong)shOff);
        BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(52), ehdrSize);
        BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(58), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(60), shCount);
        BinaryPrimitives.WriteUInt16LittleEndian(img.AsSpan(62), 5); // shstrndx

        dynsym.CopyTo(img, dynsymOff);
        dynstr.CopyTo(img, dynstrOff);
        dynamic.CopyTo(img, dynamicOff);
        rodata.CopyTo(img, rodataOff);
        shstr.CopyTo(img, shstrOff);

        void Shdr(int index, uint name, uint type, ulong addr, ulong offset, ulong size, uint link, ulong entSize)
        {
            var o = shOff + index * 64;
            BinaryPrimitives.WriteUInt32LittleEndian(img.AsSpan(o), name);
            BinaryPrimitives.WriteUInt32LittleEndian(img.AsSpan(o + 4), type);
            BinaryPrimitives.WriteUInt64LittleEndian(img.AsSpan(o + 16), addr);
            BinaryPrimitives.WriteUInt64LittleEndian(img.AsSpan(o + 24), offset);
            BinaryPrimitives.WriteUInt64LittleEndian(img.AsSpan(o + 32), size);
            BinaryPrimitives.WriteUInt32LittleEndian(img.AsSpan(o + 40), link);
            BinaryPrimitives.WriteUInt64LittleEndian(img.AsSpan(o + 56), entSize);
        }
        Shdr(1, nDynsym, 11, (ulong)dynsymOff, (ulong)dynsymOff, (ulong)dynsym.Count, 2, 24);
        Shdr(2, nDynstr, 3, dynstrAddr, (ulong)dynstrOff, (ulong)dynstr.Count, 0, 0);
        Shdr(3, nDynamic, 6, (ulong)dynamicOff, (ulong)dynamicOff, (ulong)dynamic.Count, 2, 16);
        Shdr(4, nData, 1, (ulong)rodataOff, (ulong)rodataOff, (ulong)rodata.Length, 0, 0);
        Shdr(5, nShstrtab, 3, 0, (ulong)shstrOff, (ulong)shstr.Count, 0, 0);
        return img;
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;
}
