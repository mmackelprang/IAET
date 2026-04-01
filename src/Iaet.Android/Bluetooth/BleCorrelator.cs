using Iaet.Core.Models;

namespace Iaet.Android.Bluetooth;

/// <summary>Result of correlating static BLE services with runtime HCI observations.</summary>
public sealed record BleCorrelationResult
{
    public IReadOnlyList<CorrelatedBleService> Services { get; init; } = [];
    public IReadOnlyList<AttOperation> UnmatchedOperations { get; init; } = [];
    public int StaticOnlyCount { get; init; }
    public int RuntimeOnlyCount { get; init; }
    public int CorrelatedCount { get; init; }
}

/// <summary>A BLE service with its source annotation and optional runtime observation count.</summary>
public sealed record CorrelatedBleService
{
    public required BleService Service { get; init; }

    /// <summary>"static", "runtime", or "both".</summary>
    public required string Source { get; init; }

    public ConfidenceLevel Confidence { get; init; }
    public int RuntimeObservationCount { get; init; }
}

/// <summary>
/// Correlates statically-discovered BLE services (from decompiled APK source)
/// with runtime HCI observations (from btsnoop_hci.log).
/// Handle-to-UUID mapping requires GATT discovery packets and is not yet implemented;
/// for now, runtime operations are reported as unmatched alongside static services.
/// </summary>
public static class BleCorrelator
{
    /// <summary>
    /// Correlate static BLE services with HCI log observations.
    /// </summary>
    public static BleCorrelationResult Correlate(
        IReadOnlyList<BleService> staticServices,
        HciLogResult? hciResult)
    {
        ArgumentNullException.ThrowIfNull(staticServices);

        if (hciResult is null || hciResult.Operations.Count == 0)
        {
            return new BleCorrelationResult
            {
                Services = staticServices.Select(s => new CorrelatedBleService
                {
                    Service = s,
                    Source = "static",
                    Confidence = ConfidenceLevel.Medium,
                }).ToList(),
                StaticOnlyCount = staticServices.Count,
            };
        }

        // Group HCI operations by handle to count distinct runtime handles
        var handleCounts = hciResult.Operations
            .GroupBy(o => o.Handle)
            .ToDictionary(g => g.Key, g => g.Count());

        // Until handle-to-UUID mapping is implemented via GATT discovery parsing,
        // we report static services separately alongside all runtime operations
        var correlated = staticServices.Select(s => new CorrelatedBleService
        {
            Service = s,
            Source = "static",
            Confidence = s.Confidence,
        }).ToList();

        return new BleCorrelationResult
        {
            Services = correlated,
            UnmatchedOperations = hciResult.Operations,
            StaticOnlyCount = staticServices.Count,
            RuntimeOnlyCount = handleCounts.Count,
            CorrelatedCount = 0, // handle-to-UUID mapping not yet implemented
        };
    }
}
