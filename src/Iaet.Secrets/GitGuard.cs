namespace Iaet.Secrets;

/// <summary>
/// Ensures the repository .gitignore contains the pattern that prevents committing .env.iaet secrets files.
/// </summary>
public static class GitGuard
{
    private const string GitignoreEntry = ".iaet-projects/**/.env.iaet";

    /// <summary>
    /// Idempotently adds <c>.iaet-projects/**/.env.iaet</c> to the .gitignore in <paramref name="repoDirectory"/>.
    /// Creates the .gitignore if it does not exist.
    /// </summary>
    public static void EnsureGitignore(string repoDirectory)
    {
        var gitignorePath = Path.Combine(repoDirectory, ".gitignore");

        if (File.Exists(gitignorePath))
        {
            var existing = File.ReadAllText(gitignorePath);
            if (existing.Contains(GitignoreEntry, StringComparison.Ordinal))
                return;

            // Ensure there is a trailing newline before appending
            var separator = existing.Length > 0 && existing[^1] != '\n' ? "\n" : string.Empty;
            File.AppendAllText(gitignorePath, $"{separator}{GitignoreEntry}\n");
        }
        else
        {
            File.WriteAllText(gitignorePath, $"{GitignoreEntry}\n");
        }
    }
}
