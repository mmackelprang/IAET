namespace Iaet.Core.Abstractions;

public interface ISecretsRedactor
{
    string Redact(string input, string projectName);
    Task<string> RedactAsync(string input, string projectName, CancellationToken ct = default);
}
