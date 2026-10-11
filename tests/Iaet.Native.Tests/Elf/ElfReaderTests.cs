using FluentAssertions;
using Iaet.Native.Elf;

namespace Iaet.Native.Tests.Elf;

public sealed class ElfReaderTests
{
    private static readonly ElfTestImage.Sym[] Symbols =
    [
        new("Java_com_example_ble_NativeBridge_sendCommand", Defined: true, IsFunction: true, 0x2000),
        new("Java_com_example_ble_NativeBridge_parseFrame", Defined: true, IsFunction: true, 0x2100),
        new("JNI_OnLoad", Defined: true, IsFunction: true, 0x1F00),
        new("g_protocol_version", Defined: true, IsFunction: false, 0x9000),
        new("SSL_CTX_new", Defined: false, IsFunction: true),
        new("__android_log_print", Defined: false, IsFunction: true),
        new("malloc", Defined: false, IsFunction: true),
    ];

    [Fact]
    public void Parse_reads_architecture_type_and_endianness()
    {
        var img = ElfTestImage.Build(Symbols, ["libc.so", "liblog.so"], soName: "libbridge.so");

        var info = ElfReader.Parse(img);

        info.Bitness.Should().Be(64);
        info.LittleEndian.Should().BeTrue();
        info.Architecture.Should().Be("aarch64");
        info.Type.Should().Be("shared-object");
        info.SoName.Should().Be("libbridge.so");
    }

    [Fact]
    public void Parse_separates_imports_from_exports()
    {
        var img = ElfTestImage.Build(Symbols, ["libc.so"]);

        var info = ElfReader.Parse(img);

        info.Exports.Select(s => s.Name).Should().BeEquivalentTo(
            "Java_com_example_ble_NativeBridge_sendCommand",
            "Java_com_example_ble_NativeBridge_parseFrame",
            "JNI_OnLoad",
            "g_protocol_version");
        info.Imports.Select(s => s.Name).Should().BeEquivalentTo("SSL_CTX_new", "__android_log_print", "malloc");
        info.Exports.Single(s => s.Name == "g_protocol_version").Type.Should().Be("object");
        info.Exports.Single(s => s.Name == "JNI_OnLoad").Address.Should().Be(0x1F00);
        info.Imports.Should().OnlyContain(s => s.Address == null);
    }

    [Fact]
    public void Parse_reads_needed_libraries_from_dynamic_section()
    {
        var img = ElfTestImage.Build(Symbols, ["libc.so", "liblog.so", "libssl.so"]);

        var info = ElfReader.Parse(img);

        info.NeededLibraries.Should().Equal("libc.so", "liblog.so", "libssl.so");
        info.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Parse_reports_warning_when_section_table_missing()
    {
        var img = ElfTestImage.Build(Symbols, []);
        // Zero the section header offset to simulate a stripped/packed image.
        Array.Clear(img, 40, 8);

        var info = ElfReader.Parse(img);

        info.Imports.Should().BeEmpty();
        info.Exports.Should().BeEmpty();
        info.Warnings.Should().ContainSingle(w => w.Contains("Section header table", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_rejects_non_elf_input()
    {
        var act = () => ElfReader.Parse("MZ this is not an ELF file at all, just padding bytes..........."u8.ToArray());

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_maps_x86_64_machine()
    {
        var img = ElfTestImage.Build(Symbols, [], machine: 0x3E);

        ElfReader.Parse(img).Architecture.Should().Be("x86_64");
    }
}
