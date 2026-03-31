using System.Globalization;
using System.Text;
using Iaet.Core.Models;

namespace Iaet.Diagrams;

public static class SequenceDiagramGenerator
{
    public static string Generate(string title, IReadOnlyList<CapturedRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var sb = new StringBuilder();
        sb.AppendLine("sequenceDiagram");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    title {title}");

        if (requests.Count == 0)
        {
            sb.AppendLine("    Note over Browser: No requests captured");
            return sb.ToString();
        }

        sb.AppendLine("    participant Browser");

        var hosts = requests
            .Select(r => GetHost(r.Url))
            .Where(h => h is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var host in hosts)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    participant {SanitizeParticipant(host!)}");
        }

        var ordered = requests.OrderBy(r => r.Timestamp).ToList();
        foreach (var req in ordered)
        {
            var host = GetHost(req.Url);
            if (host is null) continue;

            var participant = SanitizeParticipant(host);
            var path = GetPath(req.Url);
            var arrow = req.ResponseStatus >= 400 ? "-->>" : "->>";
            var returnArrow = req.ResponseStatus >= 400 ? "--x" : "-->>";

            sb.AppendLine(CultureInfo.InvariantCulture, $"    Browser{arrow}{participant}: {req.HttpMethod} {path}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {participant}{returnArrow}Browser: {req.ResponseStatus} ({req.DurationMs}ms)");
        }

        return sb.ToString();
    }

    private static string? GetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        return uri.Host;
    }

    private static string GetPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;
        var path = uri.AbsolutePath;
        return path.Length > 40 ? path[..40] + "..." : path;
    }

    private static string SanitizeParticipant(string name) =>
        name.Replace('.', '_').Replace('-', '_');
}
