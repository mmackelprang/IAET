using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public sealed class ClientPromptGeneratorTests
{
    [Fact]
    public void Generate_includes_target_info()
    {
        var ctx = TestContextFactory.MakeContext();
        var prompt = ClientPromptGenerator.Generate(ctx);

        prompt.Should().Contain("API Client Generation Request");
        prompt.Should().Contain("TestApp");
    }

    [Fact]
    public void Generate_includes_endpoints()
    {
        var ctx = TestContextFactory.MakeContext();
        var prompt = ClientPromptGenerator.Generate(ctx);

        prompt.Should().Contain("GET /api/users/{id}");
    }

    [Fact]
    public void Generate_includes_language()
    {
        var ctx = TestContextFactory.MakeContext();
        var prompt = ClientPromptGenerator.Generate(ctx, "Python");

        prompt.Should().Contain("**Python**");
    }

    [Fact]
    public void Generate_includes_streams_when_present()
    {
        var ctx = TestContextFactory.MakeContextWithStreams();
        var prompt = ClientPromptGenerator.Generate(ctx);

        prompt.Should().Contain("Streaming Protocols");
        prompt.Should().Contain("WebSocket");
    }
}
