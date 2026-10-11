using System.Text;
using FluentAssertions;
using Iaet.Native.Extractors;

namespace Iaet.Native.Tests.Extractors;

public sealed class BinaryStringExtractorTests
{
    [Fact]
    public void Extract_finds_ascii_runs_and_records_offsets()
    {
        var bytes = new List<byte> { 0x00, 0x01 };
        bytes.AddRange(Encoding.ASCII.GetBytes("https://api.example.com/v1"));
        bytes.AddRange([0x00, 0xFF, 0x02]);
        bytes.AddRange(Encoding.ASCII.GetBytes("abc")); // too short
        bytes.Add(0x00);
        bytes.AddRange(Encoding.ASCII.GetBytes("protocol_v2"));

        var strings = BinaryStringExtractor.Extract(bytes.ToArray(), minLength: 5);

        strings.Select(s => s.Value).Should().Equal("https://api.example.com/v1", "protocol_v2");
        strings[0].Offset.Should().Be(2);
        strings[0].Encoding.Should().Be("ascii");
    }

    [Fact]
    public void Extract_finds_utf16le_runs()
    {
        var bytes = new List<byte> { 0x00, 0x00 };
        bytes.AddRange(Encoding.Unicode.GetBytes("WideStringValue"));
        bytes.AddRange([0x00, 0x00, 0x7F, 0x7F]);

        var strings = BinaryStringExtractor.Extract(bytes.ToArray(), minLength: 5);

        strings.Should().Contain(s => s.Value == "WideStringValue" && s.Encoding == "utf-16le");
    }

    [Fact]
    public void Extract_respects_max_strings()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 100; i++)
            sb.Append("string_").Append(i).Append('\0');

        var strings = BinaryStringExtractor.Extract(Encoding.ASCII.GetBytes(sb.ToString()), minLength: 5, maxStrings: 10);

        strings.Should().HaveCount(10);
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        BinaryStringExtractor.Extract([]).Should().BeEmpty();
    }
}
