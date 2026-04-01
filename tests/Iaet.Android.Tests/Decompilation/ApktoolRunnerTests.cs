using FluentAssertions;
using Iaet.Android.Decompilation;

namespace Iaet.Android.Tests.Decompilation;

public sealed class ApktoolRunnerTests
{
    [Fact]
    public void BuildArguments_produces_decode_command()
    {
        var args = ApktoolRunner.BuildArguments("/path/to/app.apk", "/output/dir");

        args.Should().Contain("d");
        args.Should().Contain("-o");
        args.Should().Contain("/output/dir");
        args.Should().Contain("-f");
        args.Should().Contain("/path/to/app.apk");
    }

    [Fact]
    public async Task RunAsync_throws_when_apktool_not_found()
    {
        var runner = new ApktoolRunner("nonexistent-apktool-binary-xyz");

        var act = () => runner.RunAsync("/fake.apk", "/fake-out");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
