using System.Globalization;
using System.Text;

namespace Iaet.Export.Generators;

public static class SecretsAuditGenerator
{
    public static string Generate(string projectName, IReadOnlyDictionary<string, string> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        var sb = new StringBuilder();
        sb.AppendLine("# Secrets Audit");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Project:** {projectName}");
        sb.AppendLine();

        if (secrets.Count == 0)
        {
            sb.AppendLine("No secrets stored for this project.");
            return sb.ToString();
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"**Total secrets:** {secrets.Count}");
        sb.AppendLine();
        sb.AppendLine("> **Note:** Secret values are never included in this audit. Only key names and metadata are shown.");
        sb.AppendLine();

        sb.AppendLine("| Key | Length | Status |");
        sb.AppendLine("|-----|--------|--------|");
        foreach (var (key, value) in secrets.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| `{key}` | {value.Length} chars | Active |");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
