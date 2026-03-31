using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class SecretsAuditGeneratorTests
{
    [Fact]
    public void Generate_lists_secret_keys_without_values()
    {
        var secrets = new Dictionary<string, string>
        {
            ["SESSION_COOKIE"] = "super_secret_value_123",
            ["AUTH_TOKEN"] = "eyJhbGciOiJIUzI1NiJ9.payload.sig",
        };

        var markdown = SecretsAuditGenerator.Generate("google-voice", secrets);

        markdown.Should().Contain("# Secrets Audit");
        markdown.Should().Contain("SESSION_COOKIE");
        markdown.Should().Contain("AUTH_TOKEN");
        markdown.Should().NotContain("super_secret_value_123");
        markdown.Should().NotContain("eyJhbGciOiJIUzI1NiJ9");
    }

    [Fact]
    public void Generate_shows_value_lengths()
    {
        var secrets = new Dictionary<string, string>
        {
            ["SHORT_KEY"] = "abc",
            ["LONG_KEY"] = "a]very_long_secret_value_that_is_over_20_chars",
        };

        var markdown = SecretsAuditGenerator.Generate("proj", secrets);

        markdown.Should().Contain("3 chars");
        markdown.Should().Contain("46 chars");
    }

    [Fact]
    public void Generate_handles_empty()
    {
        var markdown = SecretsAuditGenerator.Generate("proj", new Dictionary<string, string>());

        markdown.Should().Contain("No secrets");
    }
}
