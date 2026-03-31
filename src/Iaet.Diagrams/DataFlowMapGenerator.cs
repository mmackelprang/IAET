using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class DataFlowMapGenerator
{
    public static string Generate(string title, IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    %% {title}");

        if (requests.Count == 0)
            return sb.ToString();

        sb.AppendLine("    Browser([Browser])");

        var hostCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in requests)
        {
            var host = GetHost(req.Url);
            if (host is null) continue;

            if (!hostCounts.TryGetValue(host, out var count))
                count = 0;
            hostCounts[host] = count + 1;
        }

        foreach (var (host, count) in hostCounts)
        {
            var id = SanitizeId(host);
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {id}[{host}]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    Browser -->|{count} requests| {id}");
        }

        return sb.ToString();
    }

    private static string? GetHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

    private static string SanitizeId(string name) =>
        name.Replace('.', '_').Replace('-', '_');
}
