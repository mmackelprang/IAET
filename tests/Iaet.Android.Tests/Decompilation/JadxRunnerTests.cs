using FluentAssertions;
using Iaet.Android.Decompilation;

namespace Iaet.Android.Tests.Decompilation;

public sealed class JadxRunnerTests
{
    [Fact]
    public void BuildArguments_produces_correct_command()
    {
        var args = JadxRunner.BuildArguments("/path/to/app.apk", "/output/dir");

        args.Should().Contain("-d");
        args.Should().Contain("/output/dir");
        args.Should().Contain("/path/to/app.apk");
    }

    [Fact]
    public void BuildArguments_includes_no_imports_flag()
    {
        var args = JadxRunner.BuildArguments("/app.apk", "/out");

        args.Should().Contain("--no-imports");
    }

    [Fact]
    public async Task RunAsync_throws_when_jadx_not_found()
    {
        var runner = new JadxRunner("nonexistent-jadx-binary-xyz");

        var act = () => runner.RunAsync("/fake.apk", "/fake-out");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
