using Iaet.Core.Abstractions;

namespace Iaet.Secrets;

/// <summary>
/// Replaces secret values found in a string with &lt;REDACTED:KEY&gt; markers.
/// Skips values shorter than 4 characters to avoid false positives.
/// </summary>
public sealed class SecretsRedactor : ISecretsRedactor
{
    private const int MinSecretLength = 4;

    private readonly ISecretsStore _store;

    public SecretsRedactor(ISecretsStore store)
    {
        _store = store;
    }

    public string Redact(string input, string projectName)
        => RedactAsync(input, projectName).GetAwaiter().GetResult();

    public async Task<string> RedactAsync(string input, string projectName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var secrets = await _store.ListAsync(projectName, ct).ConfigureAwait(false);

        // Sort longest value first to avoid partial replacements masking longer matches
        var ordered = secrets
            .Where(kv => kv.Value.Length >= MinSecretLength)
            .OrderByDescending(kv => kv.Value.Length);

        var result = input;
        foreach (var (key, value) in ordered)
            result = result.Replace(value, $"<REDACTED:{key}>", StringComparison.Ordinal);

        return result;
    }
}
