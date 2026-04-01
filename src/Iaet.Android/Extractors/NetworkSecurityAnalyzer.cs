using System.Xml.Linq;
using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

public static class NetworkSecurityAnalyzer
{
    public static NetworkSecurityConfig Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new NetworkSecurityConfig();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
#pragma warning disable CA1031
        catch (Exception)
        {
            return new NetworkSecurityConfig();
        }
#pragma warning restore CA1031

        var root = doc.Root;
        if (root is null)
            return new NetworkSecurityConfig();

        // Base config cleartext
        var baseConfig = root.Element("base-config");
        var cleartextDefault = true;
        if (baseConfig?.Attribute("cleartextTrafficPermitted")?.Value == "false")
            cleartextDefault = false;

        var pinnedDomains = new List<PinnedDomain>();
        var cleartextDomains = new List<string>();

        foreach (var domainConfig in root.Elements("domain-config"))
        {
            var domains = domainConfig.Elements("domain")
                .Select(d => d.Value.Trim())
                .ToList();

            // Check for pins
            var pinSet = domainConfig.Element("pin-set");
            if (pinSet is not null)
            {
                var pins = pinSet.Elements("pin")
                    .Select(p => p.Value.Trim())
                    .ToList();

                foreach (var domain in domains)
                {
                    pinnedDomains.Add(new PinnedDomain { Domain = domain, Pins = pins });
                }
            }

            // Check for cleartext
            if (domainConfig.Attribute("cleartextTrafficPermitted")?.Value == "true")
            {
                cleartextDomains.AddRange(domains);
            }
        }

        return new NetworkSecurityConfig
        {
            PinnedDomains = pinnedDomains,
            CleartextPermittedDomains = cleartextDomains,
            CleartextDefaultPermitted = cleartextDefault,
        };
    }

    public static NetworkSecurityConfig ParseFile(string path)
    {
        if (!File.Exists(path))
            return new NetworkSecurityConfig();

        return Parse(File.ReadAllText(path));
    }
}
