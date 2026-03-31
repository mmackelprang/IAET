using FluentAssertions;
using Iaet.Secrets;

namespace Iaet.Secrets.Tests;

public sealed class GitGuardTests : IDisposable
{
    private readonly string _repoDir;

    public GitGuardTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_repoDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoDir))
            Directory.Delete(_repoDir, recursive: true);
    }

    [Fact]
    public void EnsureGitignore_creates_file_with_env_pattern()
    {
        GitGuard.EnsureGitignore(_repoDir);
        var gitignorePath = Path.Combine(_repoDir, ".gitignore");
        File.Exists(gitignorePath).Should().BeTrue();
        var content = File.ReadAllText(gitignorePath);
        content.Should().Contain(".env.iaet");
    }

    [Fact]
    public void EnsureGitignore_appends_to_existing_file()
    {
        var gitignorePath = Path.Combine(_repoDir, ".gitignore");
        File.WriteAllText(gitignorePath, "node_modules/\n");
        GitGuard.EnsureGitignore(_repoDir);
        var content = File.ReadAllText(gitignorePath);
        content.Should().Contain("node_modules/");
        content.Should().Contain(".env.iaet");
    }

    [Fact]
    public void EnsureGitignore_is_idempotent()
    {
        GitGuard.EnsureGitignore(_repoDir);
        GitGuard.EnsureGitignore(_repoDir);
        var content = File.ReadAllText(Path.Combine(_repoDir, ".gitignore"));
        var count = content.Split(".env.iaet").Length - 1;
        count.Should().Be(1);
    }

    [Fact]
    public void EnsureGitignore_includes_projects_directory()
    {
        GitGuard.EnsureGitignore(_repoDir);
        var content = File.ReadAllText(Path.Combine(_repoDir, ".gitignore"));
        content.Should().Contain(".iaet-projects/**/.env.iaet");
    }
}
