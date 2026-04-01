namespace Iaet.Android.Bluetooth;

/// <summary>
/// Lookup table for standard Bluetooth SIG GATT service and characteristic UUIDs.
/// Maps short UUIDs (0x180D) and full UUIDs to human-readable names.
/// </summary>
public static class BleSigLookup
{
    // Keys are stored in uppercase to satisfy CA1308 (prefer ToUpperInvariant over ToLowerInvariant).
    private static readonly Dictionary<string, string> Services = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1800"] = "Generic Access",
        ["1801"] = "Generic Attribute",
        ["1802"] = "Immediate Alert",
        ["1803"] = "Link Loss",
        ["1804"] = "Tx Power",
        ["1805"] = "Current Time",
        ["1806"] = "Reference Time Update",
        ["1807"] = "Next DST Change",
        ["1808"] = "Glucose",
        ["1809"] = "Health Thermometer",
        ["180A"] = "Device Information",
        ["180D"] = "Heart Rate",
        ["180E"] = "Phone Alert Status",
        ["180F"] = "Battery Service",
        ["1810"] = "Blood Pressure",
        ["1811"] = "Alert Notification",
        ["1812"] = "Human Interface Device",
        ["1813"] = "Scan Parameters",
        ["1814"] = "Running Speed and Cadence",
        ["1815"] = "Automation IO",
        ["1816"] = "Cycling Speed and Cadence",
        ["1818"] = "Cycling Power",
        ["1819"] = "Location and Navigation",
        ["181A"] = "Environmental Sensing",
        ["181B"] = "Body Composition",
        ["181C"] = "User Data",
        ["181D"] = "Weight Scale",
        ["181E"] = "Bond Management",
        ["181F"] = "Continuous Glucose Monitoring",
        ["1820"] = "Internet Protocol Support",
        ["1821"] = "Indoor Positioning",
        ["1822"] = "Pulse Oximeter",
        ["1823"] = "HTTP Proxy",
        ["1824"] = "Transport Discovery",
        ["1825"] = "Object Transfer",
        ["1826"] = "Fitness Machine",
        ["1827"] = "Mesh Provisioning",
        ["1828"] = "Mesh Proxy",
        ["1829"] = "Reconnection Configuration",
    };

    private static readonly Dictionary<string, string> Characteristics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["2A00"] = "Device Name",
        ["2A01"] = "Appearance",
        ["2A02"] = "Peripheral Privacy Flag",
        ["2A04"] = "Peripheral Preferred Connection Parameters",
        ["2A05"] = "Service Changed",
        ["2A19"] = "Battery Level",
        ["2A1C"] = "Temperature Measurement",
        ["2A1D"] = "Temperature Type",
        ["2A1E"] = "Intermediate Temperature",
        ["2A23"] = "System ID",
        ["2A24"] = "Model Number String",
        ["2A25"] = "Serial Number String",
        ["2A26"] = "Firmware Revision String",
        ["2A27"] = "Hardware Revision String",
        ["2A28"] = "Software Revision String",
        ["2A29"] = "Manufacturer Name String",
        ["2A37"] = "Heart Rate Measurement",
        ["2A38"] = "Body Sensor Location",
        ["2A39"] = "Heart Rate Control Point",
        ["2A49"] = "Blood Pressure Feature",
        ["2A4D"] = "Report",
        ["2A50"] = "PnP ID",
        ["2A6E"] = "Temperature",
        ["2A6F"] = "Humidity",
        ["2A76"] = "UV Index",
        ["2A77"] = "Irradiance",
        ["2A78"] = "Rainfall",
        ["2A79"] = "Wind Chill",
    };

    /// <summary>Try to find a standard name for a BLE service UUID. Returns null if not found.</summary>
    public static string? LookupService(string uuid)
    {
        ArgumentNullException.ThrowIfNull(uuid);
        var shortUuid = ExtractShortUuid(uuid);
        return shortUuid is not null && Services.TryGetValue(shortUuid, out var name) ? name : null;
    }

    /// <summary>Try to find a standard name for a BLE characteristic UUID.</summary>
    public static string? LookupCharacteristic(string uuid)
    {
        ArgumentNullException.ThrowIfNull(uuid);
        var shortUuid = ExtractShortUuid(uuid);
        return shortUuid is not null && Characteristics.TryGetValue(shortUuid, out var name) ? name : null;
    }

    /// <summary>Check if a UUID is a standard Bluetooth SIG UUID (base UUID pattern).</summary>
    public static bool IsStandardUuid(string uuid)
    {
        ArgumentNullException.ThrowIfNull(uuid);
        return uuid.EndsWith("-0000-1000-8000-00805f9b34fb", StringComparison.OrdinalIgnoreCase)
            || (uuid.Length == 4 && IsHex(uuid));
    }

    private static string? ExtractShortUuid(string uuid)
    {
        // Handle full UUID: 0000180d-0000-1000-8000-00805f9b34fb -> 180D
        if (uuid.Length == 36 && uuid[8] == '-')
            return uuid[..8].TrimStart('0').ToUpperInvariant();

        // Handle 0x prefix: 0x180D -> 180D
        if (uuid.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uuid[2..].ToUpperInvariant();

        // Handle short: 180D -> 180D
        if (uuid.Length <= 4 && IsHex(uuid))
            return uuid.ToUpperInvariant();

        return null;
    }

    private static bool IsHex(string s) => s.All(c => char.IsAsciiHexDigit(c));
}
