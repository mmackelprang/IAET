using FluentAssertions;
using Iaet.Export.Generators;

namespace Iaet.Export.Tests.Generators;

public class CSharpClientGeneratorTests
{
    [Fact]
    public void Generate_ContainsNamespace()
    {
        var ctx = TestContextFactory.MakeContext();
        var cs  = CSharpClientGenerator.Generate(ctx);

        cs.Should().Contain("namespace");
        cs.Should().Contain("TestApp");
    }

    [Fact]
    public void Generate_ContainsRecordType()
    {
        var ctx = TestContextFactory.MakeContext();
        var cs  = CSharpClientGenerator.Generate(ctx);

        // The CSharpRecord from the schema result contains "UsersResponse"
        cs.Should().Contain("UsersResponse");
    }

    [Fact]
    public void Generate_ContainsMethodPerEndpoint()
    {
        var ctx = TestContextFactory.MakeContext();
        var cs  = CSharpClientGenerator.Generate(ctx);

        // GET /api/users/{id} → GetApiUsersIdAsync
        cs.Should().Contain("Async");
        cs.Should().Contain("HttpClient");
    }
}
