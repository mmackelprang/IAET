using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public sealed class BleModelTests
{
    [Fact]
    public void BleService_holds_service_metadata()
    {
        var service = new BleService
        {
            Uuid = "0000180d-0000-1000-8000-00805f9b34fb",
            Name = "Heart Rate",
            IsStandardService = true,
            Characteristics =
            [
                new BleCharacteristic
                {
                    Uuid = "00002a37-0000-1000-8000-00805f9b34fb",
                    Name = "Heart Rate Measurement",
                    Operations = [BleOperationType.Notify],
                },
            ],
            SourceFile = "HeartRateService.java",
            Confidence = ConfidenceLevel.High,
        };

        service.Uuid.Should().Be("0000180d-0000-1000-8000-00805f9b34fb");
        service.Name.Should().Be("Heart Rate");
        service.IsStandardService.Should().BeTrue();
        service.Characteristics.Should().HaveCount(1);
        service.SourceFile.Should().Be("HeartRateService.java");
        service.Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void BleService_defaults_are_correct()
    {
        var service = new BleService { Uuid = "custom-uuid" };

        service.Name.Should().BeNull();
        service.IsStandardService.Should().BeFalse();
        service.Characteristics.Should().BeEmpty();
        service.SourceFile.Should().BeNull();
        service.Confidence.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public void BleCharacteristic_holds_characteristic_metadata()
    {
        var characteristic = new BleCharacteristic
        {
            Uuid = "00002a37-0000-1000-8000-00805f9b34fb",
            Name = "Heart Rate Measurement",
            Operations = [BleOperationType.Read, BleOperationType.Notify],
            DataFormat = "uint8",
            SourceFile = "HrMonitor.java",
        };

        characteristic.Uuid.Should().Be("00002a37-0000-1000-8000-00805f9b34fb");
        characteristic.Name.Should().Be("Heart Rate Measurement");
        characteristic.Operations.Should().HaveCount(2);
        characteristic.Operations.Should().Contain(BleOperationType.Read);
        characteristic.Operations.Should().Contain(BleOperationType.Notify);
        characteristic.DataFormat.Should().Be("uint8");
        characteristic.SourceFile.Should().Be("HrMonitor.java");
    }

    [Fact]
    public void BleCharacteristic_defaults_are_correct()
    {
        var characteristic = new BleCharacteristic { Uuid = "custom-uuid" };

        characteristic.Name.Should().BeNull();
        characteristic.Operations.Should().BeEmpty();
        characteristic.DataFormat.Should().BeNull();
        characteristic.SourceFile.Should().BeNull();
    }

    [Fact]
    public void BleOperationType_has_all_expected_values()
    {
        Enum.GetValues<BleOperationType>().Should().HaveCount(5);
        Enum.GetValues<BleOperationType>().Should().Contain(BleOperationType.Read);
        Enum.GetValues<BleOperationType>().Should().Contain(BleOperationType.Write);
        Enum.GetValues<BleOperationType>().Should().Contain(BleOperationType.WriteNoResponse);
        Enum.GetValues<BleOperationType>().Should().Contain(BleOperationType.Notify);
        Enum.GetValues<BleOperationType>().Should().Contain(BleOperationType.Indicate);
    }

    [Fact]
    public void BleDataFlow_holds_data_flow_metadata()
    {
        var flow = new BleDataFlow
        {
            CharacteristicUuid = "00002a37-0000-1000-8000-00805f9b34fb",
            CallbackLocation = "HeartRateCallback.java:42",
            ParsingDescription = "Reads uint8 heart rate value from byte[1]",
            VariableName = "heartRateBpm",
            UiBinding = "heartRateTextView",
            InferredMeaning = "Current heart rate in BPM",
            Confidence = ConfidenceLevel.Medium,
        };

        flow.CharacteristicUuid.Should().Be("00002a37-0000-1000-8000-00805f9b34fb");
        flow.CallbackLocation.Should().Be("HeartRateCallback.java:42");
        flow.ParsingDescription.Should().Be("Reads uint8 heart rate value from byte[1]");
        flow.VariableName.Should().Be("heartRateBpm");
        flow.UiBinding.Should().Be("heartRateTextView");
        flow.InferredMeaning.Should().Be("Current heart rate in BPM");
        flow.Confidence.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public void BleDataFlow_defaults_are_correct()
    {
        var flow = new BleDataFlow { CharacteristicUuid = "test-uuid" };

        flow.CallbackLocation.Should().BeNull();
        flow.ParsingDescription.Should().BeNull();
        flow.VariableName.Should().BeNull();
        flow.UiBinding.Should().BeNull();
        flow.InferredMeaning.Should().BeNull();
        flow.Confidence.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void BleService_with_multiple_characteristics()
    {
        var service = new BleService
        {
            Uuid = "0000180d-0000-1000-8000-00805f9b34fb",
            Characteristics =
            [
                new BleCharacteristic { Uuid = "00002a37-0000-1000-8000-00805f9b34fb", Operations = [BleOperationType.Notify] },
                new BleCharacteristic { Uuid = "00002a38-0000-1000-8000-00805f9b34fb", Operations = [BleOperationType.Read] },
                new BleCharacteristic { Uuid = "00002a39-0000-1000-8000-00805f9b34fb", Operations = [BleOperationType.Write] },
            ],
        };

        service.Characteristics.Should().HaveCount(3);
    }
}
