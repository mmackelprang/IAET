using System.Xml.Linq;
using Iaet.Core.Models;

namespace Iaet.Android.Extractors;

public static class ManifestAnalyzer
{
    private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";

    public static ApkInfo Parse(string manifestXml)
    {
        if (string.IsNullOrWhiteSpace(manifestXml))
            return new ApkInfo { PackageName = "unknown" };

        XDocument doc;
        try
        {
            doc = XDocument.Parse(manifestXml);
        }
#pragma warning disable CA1031
        catch (Exception)
        {
            return new ApkInfo { PackageName = "unknown" };
        }
#pragma warning restore CA1031

        var manifest = doc.Root;
        if (manifest is null)
            return new ApkInfo { PackageName = "unknown" };

        var packageName = manifest.Attribute("package")?.Value ?? "unknown";
        var versionName = manifest.Attribute(AndroidNs + "versionName")?.Value;
        _ = int.TryParse(manifest.Attribute(AndroidNs + "versionCode")?.Value, out var versionCode);

        var usesSdk = manifest.Element("uses-sdk");
        _ = int.TryParse(usesSdk?.Attribute(AndroidNs + "minSdkVersion")?.Value, out var minSdk);
        _ = int.TryParse(usesSdk?.Attribute(AndroidNs + "targetSdkVersion")?.Value, out var targetSdk);

        var permissions = manifest.Elements("uses-permission")
            .Select(e => e.Attribute(AndroidNs + "name")?.Value)
            .Where(p => p is not null)
            .Cast<string>()
            .ToList();

        var app = manifest.Element("application");

        var exportedServices = ExtractExportedComponents(app, "service");
        var exportedReceivers = ExtractExportedComponents(app, "receiver");
        var exportedProviders = ExtractExportedComponents(app, "provider");

        return new ApkInfo
        {
            PackageName = packageName,
            VersionName = versionName,
            VersionCode = versionCode > 0 ? versionCode : null,
            MinSdk = minSdk > 0 ? minSdk : null,
            TargetSdk = targetSdk > 0 ? targetSdk : null,
            Permissions = permissions,
            ExportedServices = exportedServices,
            ExportedReceivers = exportedReceivers,
            ExportedProviders = exportedProviders,
        };
    }

    public static ApkInfo ParseFile(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return new ApkInfo { PackageName = "unknown" };

        return Parse(File.ReadAllText(manifestPath));
    }

    private static List<string> ExtractExportedComponents(XElement? app, string elementName)
    {
        if (app is null)
            return [];

        return app.Elements(elementName)
            .Where(e => e.Attribute(AndroidNs + "exported")?.Value == "true")
            .Select(e => e.Attribute(AndroidNs + "name")?.Value)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();
    }
}
