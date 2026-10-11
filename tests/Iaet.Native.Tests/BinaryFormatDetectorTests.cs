using FluentAssertions;
using Iaet.Core.Models;
using Iaet.Native.Tests.Elf;

namespace Iaet.Native.Tests;

public sealed class BinaryFormatDetectorTests
{
    [Fact]
    public void Detect_identifies_elf_aarch64()
    {
        var img = ElfTestImage.Build([], []);

        var d = BinaryFormatDetector.Detect(img);

        d.Format.Should().Be(BinaryFormat.Elf);
        d.Architecture.Should().Be("aarch64");
        d.Bitness.Should().Be(64);
        d.LittleEndian.Should().BeTrue();
    }

    [Fact]
    public void Detect_identifies_pe_x64()
    {
        var pe = new byte[0x100];
        pe[0] = (byte)'M'; pe[1] = (byte)'Z';
        pe[0x3C] = 0x80; // e_lfanew
        pe[0x80] = (byte)'P'; pe[0x81] = (byte)'E'; pe[0x82] = 0; pe[0x83] = 0;
        pe[0x84] = 0x64; pe[0x85] = 0x86; // IMAGE_FILE_MACHINE_AMD64

        var d = BinaryFormatDetector.Detect(pe);

        d.Format.Should().Be(BinaryFormat.Pe);
        d.Architecture.Should().Be("x86_64");
        d.Bitness.Should().Be(64);
    }

    [Fact]
    public void Detect_identifies_macho_arm64()
    {
        var macho = new byte[32];
        // 0xFEEDFACF little-endian magic, cputype 0x0100000C (ARM64)
        macho[0] = 0xCF; macho[1] = 0xFA; macho[2] = 0xED; macho[3] = 0xFE;
        macho[4] = 0x0C; macho[5] = 0x00; macho[6] = 0x00; macho[7] = 0x01;

        var d = BinaryFormatDetector.Detect(macho);

        d.Format.Should().Be(BinaryFormat.MachO);
        d.Architecture.Should().Be("aarch64");
        d.Bitness.Should().Be(64);
    }

    [Fact]
    public void Detect_identifies_fat_macho()
    {
        var fat = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE, 0, 0, 0, 2 };

        BinaryFormatDetector.Detect(fat).Format.Should().Be(BinaryFormat.MachOFat);
    }

    [Fact]
    public void Detect_returns_unknown_for_text()
    {
        var d = BinaryFormatDetector.Detect("hello world this is text"u8);

        d.Format.Should().Be(BinaryFormat.Unknown);
        d.Architecture.Should().Be("unknown");
    }
}
