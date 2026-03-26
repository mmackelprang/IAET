using FluentAssertions;
using Iaet.Cli.Commands;

namespace Iaet.Cli.Tests;

/// <summary>
/// Unit tests for <see cref="WizardPrompt"/> helper methods.
/// Console I/O is injected via TextReader / TextWriter so no real console is touched.
/// </summary>
public class WizardPromptTests
{
    // ── SlugifyTarget ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Spotify Web Player",     "spotify-web-player")]
    [InlineData("MyApp",                  "myapp")]
    [InlineData("  Hello World  ",        "hello-world")]
    [InlineData("API v2 (Beta)",          "api-v2-beta")]
    [InlineData("",                       "session")]
    [InlineData("   ",                    "session")]
    [InlineData("---",                    "session")]
    [InlineData("abc123",                 "abc123")]
    [InlineData("A B C",                  "a-b-c")]
    [InlineData("UPPER",                  "upper")]
    public void SlugifyTarget_ReturnsExpectedSlug(string input, string expected)
    {
        var result = WizardPrompt.SlugifyTarget(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void SlugifyTarget_DoesNotProduceLeadingOrTrailingHyphen()
    {
        var result = WizardPrompt.SlugifyTarget("!Hello World!");
        result.Should().NotStartWith("-").And.NotEndWith("-");
    }

    [Fact]
    public void SlugifyTarget_CollapsesConsecutiveSeparators()
    {
        var result = WizardPrompt.SlugifyTarget("Hello   World");
        result.Should().NotContain("--");
    }

    // ── GenerateSessionName ────────────────────────────────────────────────────

    [Fact]
    public void GenerateSessionName_ContainsSlugAndTimestamp()
    {
        var at = new DateTimeOffset(2026, 3, 26, 14, 5, 0, TimeSpan.Zero);
        var name = WizardPrompt.GenerateSessionName("Spotify Web Player", at);

        name.Should().StartWith("spotify-web-player-");
        name.Should().Contain("20260326");
        name.Should().Contain("1405");
    }

    [Fact]
    public void GenerateSessionName_UsesCurrentTimeWhenNotProvided()
    {
        var before = DateTimeOffset.UtcNow;
        var name = WizardPrompt.GenerateSessionName("test");

        // The name should at least contain the year of the current run
        name.Should().Contain(before.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── ReadString ─────────────────────────────────────────────────────────────

    [Fact]
    public void ReadString_ReturnsUserInput()
    {
        using var reader = new StringReader("my-value\n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadString("Enter something", reader: reader, writer: writer);

        result.Should().Be("my-value");
    }

    [Fact]
    public void ReadString_UsesDefaultWhenUserPressesEnter()
    {
        using var reader = new StringReader("\n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadString("Enter something", defaultValue: "default-val",
            reader: reader, writer: writer);

        result.Should().Be("default-val");
    }

    [Fact]
    public void ReadString_TrimsWhitespace()
    {
        using var reader = new StringReader("  trimmed  \n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadString("Enter something", reader: reader, writer: writer);

        result.Should().Be("trimmed");
    }

    [Fact]
    public void ReadString_LoopsUntilNonEmptyWhenNoDefault()
    {
        // First line empty, second has value
        using var reader = new StringReader("\nactual-value\n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadString("Enter something", reader: reader, writer: writer);

        result.Should().Be("actual-value");
        writer.ToString().Should().Contain("(value is required)");
    }

    [Fact]
    public void ReadString_ShowsDefaultInPrompt()
    {
        using var reader = new StringReader("\n");
        using var writer = new StringWriter();

        WizardPrompt.ReadString("Enter something", defaultValue: "my-default",
            reader: reader, writer: writer);

        writer.ToString().Should().Contain("[my-default]");
    }

    // ── ReadChoice ─────────────────────────────────────────────────────────────

    [Fact]
    public void ReadChoice_ReturnsZeroBasedIndex()
    {
        using var reader = new StringReader("2\n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadChoice("Pick one", ["Option A", "Option B", "Option C"],
            reader: reader, writer: writer);

        result.Should().Be(1); // 2 → zero-based index 1
    }

    [Fact]
    public void ReadChoice_ReturnsFirstOption()
    {
        using var reader = new StringReader("1\n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadChoice("Pick one", ["Alpha", "Beta"],
            reader: reader, writer: writer);

        result.Should().Be(0);
    }

    [Fact]
    public void ReadChoice_ReturnsLastOption()
    {
        using var reader = new StringReader("3\n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadChoice("Pick one", ["A", "B", "C"],
            reader: reader, writer: writer);

        result.Should().Be(2);
    }

    [Fact]
    public void ReadChoice_LoopsOnInvalidInput()
    {
        // "abc" is invalid, "0" is out of range, "2" is valid
        using var reader = new StringReader("abc\n0\n2\n");
        using var writer = new StringWriter();

        var result = WizardPrompt.ReadChoice("Pick one", ["First", "Second"],
            reader: reader, writer: writer);

        result.Should().Be(1);
        var output = writer.ToString();
        output.Should().Contain("Invalid selection");
    }

    [Fact]
    public void ReadChoice_PrintsAllOptions()
    {
        using var reader = new StringReader("1\n");
        using var writer = new StringWriter();

        WizardPrompt.ReadChoice("Which?", ["Alpha", "Beta", "Gamma"],
            reader: reader, writer: writer);

        var output = writer.ToString();
        output.Should().Contain("Alpha");
        output.Should().Contain("Beta");
        output.Should().Contain("Gamma");
    }

    [Fact]
    public void ReadChoice_ThrowsWhenNoOptions()
    {
        using var reader = new StringReader("1\n");
        using var writer = new StringWriter();

        var act = () => WizardPrompt.ReadChoice("Pick", [],
            reader: reader, writer: writer);

        act.Should().Throw<ArgumentException>();
    }

    // ── PrintBanner ────────────────────────────────────────────────────────────

    [Fact]
    public void PrintBanner_ContainsExpectedContent()
    {
        using var writer = new StringWriter();
        WizardPrompt.PrintBanner(writer);

        var output = writer.ToString();
        output.Should().Contain("IAET");
        output.Should().Contain("Investigation Wizard");
    }

    // ── PrintDivider ───────────────────────────────────────────────────────────

    [Fact]
    public void PrintDivider_WritesLineOfCorrectWidth()
    {
        using var writer = new StringWriter();
        WizardPrompt.PrintDivider(40, writer);

        var output = writer.ToString().TrimEnd('\r', '\n');
        output.Should().HaveLength(40);
        output.Should().Match(s => s.All(c => c == '─'));
    }
}
