using FluentAssertions;
using Iaet.Android.Bluetooth;
using Iaet.Core.Models;

namespace Iaet.Android.Tests.Bluetooth;

public sealed class BleCorrelatorTests
{
    [Fact]
    public void Correlate_with_null_hci_returns_static_only()
    {
        var services = new List<BleService>
        {
            new() { Uuid = "0000180d-0000-1000-8000-00805f9b34fb", Name = "Heart Rate" },
        };

        var result = BleCorrelator.Correlate(services, null);

        result.StaticOnlyCount.Should().Be(1);
        result.Services.Should().HaveCount(1);
        result.Services[0].Source.Should().Be("static");
        result.Services[0].Confidence.Should().Be(ConfidenceLevel.Medium);
        result.RuntimeOnlyCount.Should().Be(0);
        result.CorrelatedCount.Should().Be(0);
    }

    [Fact]
    public void Correlate_with_empty_hci_returns_static_only()
    {
        var services = new List<BleService>
        {
            new() { Uuid = "0000180f-0000-1000-8000-00805f9b34fb", Name = "Battery" },
        };
        var hci = new HciLogResult();

        var result = BleCorrelator.Correlate(services, hci);

        result.StaticOnlyCount.Should().Be(1);
        result.RuntimeOnlyCount.Should().Be(0);
        result.CorrelatedCount.Should().Be(0);
    }

    [Fact]
    public void Correlate_with_hci_operations_reports_unmatched()
    {
        var services = new List<BleService>();
        var hci = new HciLogResult
        {
            Operations =
            [
                new AttOperation
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = "Notify",
                    Handle = 0x15,
                    IsReceived = true,
                    ValueLength = 2,
                },
            ],
        };

        var result = BleCorrelator.Correlate(services, hci);

        result.UnmatchedOperations.Should().HaveCount(1);
        result.RuntimeOnlyCount.Should().Be(1);
        result.StaticOnlyCount.Should().Be(0);
    }

    [Fact]
    public void Correlate_with_both_static_and_hci_reports_both()
    {
        var services = new List<BleService>
        {
            new() { Uuid = "0000180d-0000-1000-8000-00805f9b34fb", Name = "Heart Rate", Confidence = ConfidenceLevel.High },
        };
        var hci = new HciLogResult
        {
            Operations =
            [
                new AttOperation
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = "Notify",
                    Handle = 0x15,
                    IsReceived = true,
                    ValueLength = 2,
                },
                new AttOperation
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = "Write",
                    Handle = 0x15,
                    IsReceived = false,
                    ValueLength = 1,
                },
            ],
        };

        var result = BleCorrelator.Correlate(services, hci);

        result.StaticOnlyCount.Should().Be(1);
        result.RuntimeOnlyCount.Should().Be(1); // one distinct handle
        result.CorrelatedCount.Should().Be(0); // no handle-to-UUID mapping yet
        result.Services.Should().HaveCount(1);
        result.Services[0].Confidence.Should().Be(ConfidenceLevel.High);
        result.UnmatchedOperations.Should().HaveCount(2);
    }

    [Fact]
    public void Correlate_with_multiple_handles_counts_distinct_handles()
    {
        var services = new List<BleService>();
        var hci = new HciLogResult
        {
            Operations =
            [
                new AttOperation
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = "Notify",
                    Handle = 0x0015,
                    IsReceived = true,
                    ValueLength = 1,
                },
                new AttOperation
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = "Write",
                    Handle = 0x0020,
                    IsReceived = false,
                    ValueLength = 1,
                },
                new AttOperation
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = "Notify",
                    Handle = 0x0015,
                    IsReceived = true,
                    ValueLength = 1,
                },
            ],
        };

        var result = BleCorrelator.Correlate(services, hci);

        result.RuntimeOnlyCount.Should().Be(2); // two distinct handles: 0x15 and 0x20
    }

    [Fact]
    public void Correlate_throws_on_null_services()
    {
        var act = () => BleCorrelator.Correlate(null!, null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Correlate_preserves_service_confidence_from_static()
    {
        var services = new List<BleService>
        {
            new()
            {
                Uuid = "0000180d-0000-1000-8000-00805f9b34fb",
                Name = "Heart Rate",
                Confidence = ConfidenceLevel.High,
            },
            new()
            {
                Uuid = "12345678-0000-1000-8000-00805f9b34fb",
                Name = "Custom",
                Confidence = ConfidenceLevel.Low,
            },
        };
        var hci = new HciLogResult
        {
            Operations =
            [
                new AttOperation
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Type = "Read",
                    Handle = 0x0003,
                    IsReceived = false,
                    ValueLength = 0,
                },
            ],
        };

        var result = BleCorrelator.Correlate(services, hci);

        result.Services.Should().HaveCount(2);
        result.Services[0].Confidence.Should().Be(ConfidenceLevel.High);
        result.Services[1].Confidence.Should().Be(ConfidenceLevel.Low);
    }
}
