using System.Buffers.Binary;
using Iaet.Core.Models;

namespace Iaet.Native;

/// <summary>Identifies the container format and target architecture of a native binary from its header bytes.</summary>
public sealed record BinaryDetection(BinaryFormat Format, string Architecture, int Bitness, bool LittleEndian);

public static class BinaryFormatDetector
{

    public static BinaryDetection Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4 && header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F')
            return DetectElf(header);

        if (header.Length >= 2 && header[0] == (byte)'M' && header[1] == (byte)'Z')
            return DetectPe(header);

        if (header.Length >= 4)
        {
            var magicLe = BinaryPrimitives.ReadUInt32LittleEndian(header);
            var magicBe = BinaryPrimitives.ReadUInt32BigEndian(header);
            switch (magicLe)
            {
                case 0xFEEDFACE: return DetectMachO(header, bits: 32, littleEndian: true);
                case 0xFEEDFACF: return DetectMachO(header, bits: 64, littleEndian: true);
            }
            switch (magicBe)
            {
                case 0xFEEDFACE: return DetectMachO(header, bits: 32, littleEndian: false);
                case 0xFEEDFACF: return DetectMachO(header, bits: 64, littleEndian: false);
                case 0xCAFEBABE: return new BinaryDetection(BinaryFormat.MachOFat, "universal", 0, false);
            }
        }

        return new BinaryDetection(BinaryFormat.Unknown, "unknown", 0, true);
    }

    private static BinaryDetection DetectElf(ReadOnlySpan<byte> h)
    {
        var bits = h.Length > 4 && h[4] == 2 ? 64 : 32;
        var le = h.Length <= 5 || h[5] != 2;
        if (h.Length < 20)
            return new BinaryDetection(BinaryFormat.Elf, "unknown", bits, le);

        var machine = le ? BinaryPrimitives.ReadUInt16LittleEndian(h[18..]) : BinaryPrimitives.ReadUInt16BigEndian(h[18..]);
        return new BinaryDetection(BinaryFormat.Elf, ElfMachineName(machine), bits, le);
    }

    public static string ElfMachineName(ushort machine) => machine switch
    {
        0x03 => "x86",
        0x08 => "mips",
        0x14 => "powerpc",
        0x15 => "powerpc64",
        0x28 => "arm",
        0x2A => "superh",
        0x32 => "ia64",
        0x3E => "x86_64",
        0xB7 => "aarch64",
        0xF3 => "riscv",
        0x5C => "loongarch",
        _ => $"elf-machine-0x{machine:X}",
    };

    private static BinaryDetection DetectPe(ReadOnlySpan<byte> h)
    {
        if (h.Length < 0x40)
            return new BinaryDetection(BinaryFormat.Pe, "unknown", 0, true);

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(h[0x3C..]);
        if (peOffset < 0 || peOffset + 6 > h.Length)
            return new BinaryDetection(BinaryFormat.Pe, "unknown", 0, true);

        if (!(h[peOffset] == (byte)'P' && h[peOffset + 1] == (byte)'E' && h[peOffset + 2] == 0 && h[peOffset + 3] == 0))
            return new BinaryDetection(BinaryFormat.Pe, "dos-only", 16, true);

        var machine = BinaryPrimitives.ReadUInt16LittleEndian(h[(peOffset + 4)..]);
        var (arch, bits) = machine switch
        {
            0x014C => ("x86", 32),
            0x8664 => ("x86_64", 64),
            0x01C0 or 0x01C4 => ("arm", 32),
            0xAA64 => ("aarch64", 64),
            0x0200 => ("ia64", 64),
            _ => ($"pe-machine-0x{machine:X}", 0),
        };
        return new BinaryDetection(BinaryFormat.Pe, arch, bits, true);
    }

    private static BinaryDetection DetectMachO(ReadOnlySpan<byte> h, int bits, bool littleEndian)
    {
        if (h.Length < 8)
            return new BinaryDetection(BinaryFormat.MachO, "unknown", bits, littleEndian);

        var cpuType = littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(h[4..]) : BinaryPrimitives.ReadUInt32BigEndian(h[4..]);
        var arch = (cpuType & 0x00FFFFFF) switch
        {
            7 => bits == 64 ? "x86_64" : "x86",
            12 => bits == 64 ? "aarch64" : "arm",
            18 => bits == 64 ? "powerpc64" : "powerpc",
            _ => $"macho-cpu-0x{cpuType:X}",
        };
        return new BinaryDetection(BinaryFormat.MachO, arch, bits, littleEndian);
    }
}
