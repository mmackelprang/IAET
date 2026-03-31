using System.Net;
using System.Text.RegularExpressions;

namespace Iaet.Core.Utilities;

public static class DnsResolver
{
    /// <summary>
    /// Attempts to resolve an IP address to a hostname via reverse DNS.
    /// Returns the IP string unchanged if resolution fails.
    /// </summary>
    public static async Task<string> ResolveAsync(string ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return ipAddress;

        if (!IPAddress.TryParse(ipAddress, out var ip))
            return ipAddress;

        try
        {
            var entry = await Dns.GetHostEntryAsync(ip.ToString(), ct).ConfigureAwait(false);
            return entry.HostName;
        }
#pragma warning disable CA1031 // DNS resolution can throw SocketException, PlatformNotSupportedException, and others
        catch (Exception)
        {
            return ipAddress;
        }
#pragma warning restore CA1031
    }

    private static readonly Regex IpPattern = new(
        @"\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Finds all IP addresses in a string and resolves them to hostnames.
    /// Returns a dictionary of IP → hostname mappings.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ResolveAllInTextAsync(
        string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new Dictionary<string, string>();

        var ips = new HashSet<string>();
        foreach (Match match in IpPattern.Matches(text))
        {
            var ip = match.Groups[1].Value;
            if (!ip.StartsWith("0.", StringComparison.Ordinal)
                && !ip.StartsWith("127.", StringComparison.Ordinal)
                && IPAddress.TryParse(ip, out _))
            {
                ips.Add(ip);
            }
        }

        var results = new Dictionary<string, string>();
        foreach (var ip in ips)
        {
            results[ip] = await ResolveAsync(ip, ct).ConfigureAwait(false);
        }
        return results;
    }
}
