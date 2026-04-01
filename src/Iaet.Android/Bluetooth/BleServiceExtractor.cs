using System.Text.RegularExpressions;
using Iaet.Core.Models;

namespace Iaet.Android.Bluetooth;

/// <summary>
/// Scans decompiled Java source for BLE UUIDs and GATT operations.
/// Searches for:
/// - UUID.fromString("0000180d-0000-1000-8000-00805f9b34fb")
/// - getService(UUID) / getCharacteristic(UUID)
/// - readCharacteristic / writeCharacteristic / setCharacteristicNotification
/// Obfuscation-resistant: matches by Android SDK types, not variable names.
/// </summary>
public static partial class BleServiceExtractor
{
    /// <summary>
    /// Extract BLE service information from a single Java source file.
    /// </summary>
    public static BleExtractionResult Extract(string javaSource, string sourceFile)
    {
        if (string.IsNullOrEmpty(javaSource))
            return BleExtractionResult.Empty;

        var services = new Dictionary<string, BleServiceBuilder>(StringComparer.OrdinalIgnoreCase);
        var characteristics = new Dictionary<string, BleCharacteristicBuilder>(StringComparer.OrdinalIgnoreCase);
        var lines = javaSource.Split('\n');

        // Pass 1: Find all UUIDs
        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            foreach (Match match in UuidFromStringPattern().Matches(line))
            {
                var uuid = match.Groups[1].Value;
                ClassifyUuid(uuid, sourceFile, lineIdx + 1, line, services, characteristics);
            }

            foreach (Match match in ShortUuidPattern().Matches(line))
            {
                // Only consider short UUIDs near BLE context
                if (!HasBleContext(lines, lineIdx))
                    continue;

                var shortHex = match.Groups[1].Value.ToUpperInvariant();
                var fullUuid = $"0000{shortHex}-0000-1000-8000-00805f9b34fb";
                ClassifyUuid(fullUuid, sourceFile, lineIdx + 1, line, services, characteristics);
            }
        }

        // Pass 2: Find GATT operations and associate with nearby characteristics
        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];

            if (ReadCharacteristicPattern().IsMatch(line))
                AssociateOperation(BleOperationType.Read, lines, lineIdx, characteristics);

            if (WriteCharacteristicPattern().IsMatch(line))
            {
                // Distinguish Write from WriteNoResponse
                if (WriteNoResponsePattern().IsMatch(line) || HasWriteNoResponseContext(lines, lineIdx))
                    AssociateOperation(BleOperationType.WriteNoResponse, lines, lineIdx, characteristics);
                else
                    AssociateOperation(BleOperationType.Write, lines, lineIdx, characteristics);
            }

            if (SetNotificationPattern().IsMatch(line))
            {
                // setCharacteristicNotification with true = Notify, but we also check for Indicate
                if (IndicatePattern().IsMatch(line) || HasIndicateContext(lines, lineIdx))
                    AssociateOperation(BleOperationType.Indicate, lines, lineIdx, characteristics);
                else
                    AssociateOperation(BleOperationType.Notify, lines, lineIdx, characteristics);
            }
        }

        // Build results
        return BuildResult(services, characteristics);
    }

    /// <summary>
    /// Scan all .java files in a decompiled directory for BLE services.
    /// </summary>
    public static BleExtractionResult ExtractFromDirectory(string decompiledDir)
    {
        ArgumentNullException.ThrowIfNull(decompiledDir);

        if (!Directory.Exists(decompiledDir))
            return BleExtractionResult.Empty;

        var allServices = new Dictionary<string, BleServiceBuilder>(StringComparer.OrdinalIgnoreCase);
        var allCharacteristics = new Dictionary<string, BleCharacteristicBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(decompiledDir, "*.java", SearchOption.AllDirectories))
        {
#pragma warning disable CA1849 // File scanning loop - ReadAllTextAsync not practical here without extra complexity
            var source = File.ReadAllText(file);
#pragma warning restore CA1849
            var relativePath = Path.GetRelativePath(decompiledDir, file);
            var result = Extract(source, relativePath);

            MergeResults(result, allServices, allCharacteristics);
        }

        return BuildResult(allServices, allCharacteristics);
    }

    private static void ClassifyUuid(
        string uuid,
        string sourceFile,
        int lineNumber,
        string lineText,
        Dictionary<string, BleServiceBuilder> services,
        Dictionary<string, BleCharacteristicBuilder> characteristics)
    {
        var isService = IsServiceContext(lineText) || BleSigLookup.LookupService(uuid) is not null;
        var isCharacteristic = IsCharacteristicContext(lineText) || BleSigLookup.LookupCharacteristic(uuid) is not null;

        if (isService && !isCharacteristic)
        {
            if (!services.ContainsKey(uuid))
            {
                services[uuid] = new BleServiceBuilder
                {
                    Uuid = uuid,
                    Name = BleSigLookup.LookupService(uuid),
                    IsStandard = BleSigLookup.IsStandardUuid(uuid),
                    SourceFile = sourceFile,
                };
            }
        }
        else
        {
            // Default to characteristic if ambiguous
            if (!characteristics.ContainsKey(uuid))
            {
                characteristics[uuid] = new BleCharacteristicBuilder
                {
                    Uuid = uuid,
                    Name = BleSigLookup.LookupCharacteristic(uuid),
                    SourceFile = sourceFile,
                };
            }
        }
    }

    private static bool IsServiceContext(string line)
    {
        return line.Contains("getService", StringComparison.OrdinalIgnoreCase)
            || line.Contains("SERVICE", StringComparison.Ordinal)
            || line.Contains("BluetoothGattService", StringComparison.Ordinal);
    }

    private static bool IsCharacteristicContext(string line)
    {
        return line.Contains("getCharacteristic", StringComparison.OrdinalIgnoreCase)
            || line.Contains("CHARACTERISTIC", StringComparison.Ordinal)
            || line.Contains("BluetoothGattCharacteristic", StringComparison.Ordinal);
    }

    private static bool HasBleContext(string[] lines, int lineIdx)
    {
        // Check surrounding lines (within 10 lines) for BLE-related references
        var start = Math.Max(0, lineIdx - 10);
        var end = Math.Min(lines.Length - 1, lineIdx + 10);

        for (var i = start; i <= end; i++)
        {
            if (lines[i].Contains("BluetoothGatt", StringComparison.Ordinal)
                || lines[i].Contains("BleManager", StringComparison.OrdinalIgnoreCase)
                || lines[i].Contains("GATT", StringComparison.Ordinal)
                || lines[i].Contains("UUID.fromString", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AssociateOperation(
        BleOperationType operation,
        string[] lines,
        int lineIdx,
        Dictionary<string, BleCharacteristicBuilder> characteristics)
    {
        // Look for a UUID reference near this operation (within 15 lines before/after)
        var closestUuid = FindNearestUuid(lines, lineIdx, 15);
        if (closestUuid is not null && characteristics.TryGetValue(closestUuid, out var builder))
        {
            if (!builder.Operations.Contains(operation))
                builder.Operations.Add(operation);
        }
        else if (closestUuid is not null)
        {
            // UUID found but not yet tracked as a characteristic - add it
            var newBuilder = new BleCharacteristicBuilder
            {
                Uuid = closestUuid,
                Name = BleSigLookup.LookupCharacteristic(closestUuid),
            };
            newBuilder.Operations.Add(operation);
            characteristics[closestUuid] = newBuilder;
        }
    }

    private static string? FindNearestUuid(string[] lines, int lineIdx, int radius)
    {
        string? closestUuid = null;
        var closestDistance = int.MaxValue;

        var start = Math.Max(0, lineIdx - radius);
        var end = Math.Min(lines.Length - 1, lineIdx + radius);

        for (var i = start; i <= end; i++)
        {
            foreach (Match match in UuidFromStringPattern().Matches(lines[i]))
            {
                var distance = Math.Abs(i - lineIdx);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestUuid = match.Groups[1].Value;
                }
            }
        }

        return closestUuid;
    }

    private static bool HasWriteNoResponseContext(string[] lines, int lineIdx)
    {
        var start = Math.Max(0, lineIdx - 5);
        var end = Math.Min(lines.Length - 1, lineIdx + 5);

        for (var i = start; i <= end; i++)
        {
            if (lines[i].Contains("WRITE_TYPE_NO_RESPONSE", StringComparison.Ordinal)
                || lines[i].Contains("writeType", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasIndicateContext(string[] lines, int lineIdx)
    {
        var start = Math.Max(0, lineIdx - 5);
        var end = Math.Min(lines.Length - 1, lineIdx + 5);

        for (var i = start; i <= end; i++)
        {
            if (lines[i].Contains("ENABLE_INDICATION", StringComparison.Ordinal)
                || lines[i].Contains("indicate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static BleExtractionResult BuildResult(
        Dictionary<string, BleServiceBuilder> services,
        Dictionary<string, BleCharacteristicBuilder> characteristics)
    {
        var builtServices = services.Values.Select(s => new BleService
        {
            Uuid = s.Uuid,
            Name = s.Name,
            IsStandardService = s.IsStandard,
            Characteristics = characteristics.Values
                .Where(_ => true) // All characteristics associated with the file
                .Select(c => new BleCharacteristic
                {
                    Uuid = c.Uuid,
                    Name = c.Name,
                    Operations = c.Operations.AsReadOnly(),
                    SourceFile = c.SourceFile,
                })
                .ToList(),
            SourceFile = s.SourceFile,
            Confidence = s.IsStandard ? ConfidenceLevel.High : ConfidenceLevel.Medium,
        }).ToList();

        var builtCharacteristics = characteristics.Values.Select(c => new BleCharacteristic
        {
            Uuid = c.Uuid,
            Name = c.Name,
            Operations = c.Operations.AsReadOnly(),
            SourceFile = c.SourceFile,
        }).ToList();

        return new BleExtractionResult
        {
            Services = builtServices,
            Characteristics = builtCharacteristics,
        };
    }

    private static void MergeResults(
        BleExtractionResult result,
        Dictionary<string, BleServiceBuilder> allServices,
        Dictionary<string, BleCharacteristicBuilder> allCharacteristics)
    {
        foreach (var service in result.Services)
        {
            if (!allServices.ContainsKey(service.Uuid))
            {
                allServices[service.Uuid] = new BleServiceBuilder
                {
                    Uuid = service.Uuid,
                    Name = service.Name,
                    IsStandard = service.IsStandardService,
                    SourceFile = service.SourceFile,
                };
            }
        }

        foreach (var characteristic in result.Characteristics)
        {
            if (allCharacteristics.TryGetValue(characteristic.Uuid, out var existing))
            {
                foreach (var op in characteristic.Operations)
                {
                    if (!existing.Operations.Contains(op))
                        existing.Operations.Add(op);
                }
            }
            else
            {
                var builder = new BleCharacteristicBuilder
                {
                    Uuid = characteristic.Uuid,
                    Name = characteristic.Name,
                    SourceFile = characteristic.SourceFile,
                };
                foreach (var op in characteristic.Operations)
                    builder.Operations.Add(op);
                allCharacteristics[characteristic.Uuid] = builder;
            }
        }
    }

    [GeneratedRegex("""UUID\.fromString\("([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})"\)""", RegexOptions.IgnoreCase)]
    private static partial Regex UuidFromStringPattern();

    [GeneratedRegex("""0x([0-9a-fA-F]{4})""")]
    private static partial Regex ShortUuidPattern();

    [GeneratedRegex("""readCharacteristic""", RegexOptions.IgnoreCase)]
    private static partial Regex ReadCharacteristicPattern();

    [GeneratedRegex("""writeCharacteristic""", RegexOptions.IgnoreCase)]
    private static partial Regex WriteCharacteristicPattern();

    [GeneratedRegex("""setCharacteristicNotification""", RegexOptions.IgnoreCase)]
    private static partial Regex SetNotificationPattern();

    [GeneratedRegex("""WRITE_TYPE_NO_RESPONSE""")]
    private static partial Regex WriteNoResponsePattern();

    [GeneratedRegex("""ENABLE_INDICATION""")]
    private static partial Regex IndicatePattern();

    private sealed class BleServiceBuilder
    {
        public string Uuid { get; init; } = string.Empty;
        public string? Name { get; init; }
        public bool IsStandard { get; init; }
        public string? SourceFile { get; init; }
    }

    private sealed class BleCharacteristicBuilder
    {
        public string Uuid { get; init; } = string.Empty;
        public string? Name { get; init; }
        public List<BleOperationType> Operations { get; } = [];
        public string? SourceFile { get; init; }
    }
}

/// <summary>Result of BLE service extraction from decompiled source.</summary>
public sealed record BleExtractionResult
{
    public IReadOnlyList<BleService> Services { get; init; } = [];
    public IReadOnlyList<BleCharacteristic> Characteristics { get; init; } = [];

    internal static BleExtractionResult Empty { get; } = new();
}
