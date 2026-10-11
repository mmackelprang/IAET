using FluentAssertions;
using Iaet.Native.Ghidra;

namespace Iaet.Native.Tests.Ghidra;

public sealed class GhidraExportParserTests
{
    private const string Sample = """
        {
          "scriptVersion": "1",
          "program": { "name": "libbridge.so", "executableFormat": "Executable and Linking Format (ELF)", "languageId": "AARCH64:LE:64:v8A", "compilerSpecId": "default", "imageBase": "0x100000", "sha256": "abc" },
          "memoryBlocks": [ { "name": ".text", "start": "0x101000", "size": 4096, "read": true, "write": false, "execute": true, "initialized": true } ],
          "strings": [ { "address": "0x10a000", "value": "https://api.vendor.com/v2", "charset": "US-ASCII" } ],
          "externals": [ { "name": "SSL_CTX_new", "library": "libssl.so", "type": "function" } ],
          "functions": [
            { "name": "Java_com_app_Bridge_send", "entry": "0x101000", "size": 240, "isThunk": false, "isExternal": false, "signature": "int Java_com_app_Bridge_send(void)",
              "callers": [], "callees": ["encode_frame", "SSL_write"], "strings": ["CMD:%02x"], "pseudocode": "int Java_com_app_Bridge_send(void)\n{\n  return 0;\n}\n" },
            { "name": "encode_frame", "entry": "0x101100", "size": 80, "isThunk": false, "isExternal": false, "signature": "void encode_frame(void)",
              "callers": ["Java_com_app_Bridge_send"], "callees": [], "strings": [], "pseudocode": null, "decompileError": "timeout" }
          ],
          "functionCount": 2,
          "functionsExported": 2,
          "truncated": false
        }
        """;

    [Fact]
    public void Parse_reads_program_metadata_and_functions()
    {
        var export = GhidraExportParser.Parse(Sample);

        export.ProgramName.Should().Be("libbridge.so");
        export.LanguageId.Should().Be("AARCH64:LE:64:v8A");
        export.ImageBase.Should().Be("0x100000");
        export.FunctionCount.Should().Be(2);
        export.Truncated.Should().BeFalse();
        export.Functions.Should().HaveCount(2);

        var send = export.Functions.Single(f => f.Name == "Java_com_app_Bridge_send");
        send.EntryAddress.Should().Be("0x101000");
        send.Size.Should().Be(240);
        send.Callees.Should().Equal("encode_frame", "SSL_write");
        send.ReferencedStrings.Should().Equal("CMD:%02x");
        send.Pseudocode.Should().Contain("return 0;");

        export.Functions.Single(f => f.Name == "encode_frame").Pseudocode.Should().BeNull();
        export.Strings.Should().ContainSingle().Which.Value.Should().Be("https://api.vendor.com/v2");
        export.Externals.Should().ContainSingle().Which.Type.Should().Be("function");
    }

    [Fact]
    public void Parse_tolerates_missing_sections()
    {
        var export = GhidraExportParser.Parse("""{ "scriptVersion": "1" }""");

        export.Functions.Should().BeEmpty();
        export.Strings.Should().BeEmpty();
        export.FunctionCount.Should().Be(0);
    }
}
