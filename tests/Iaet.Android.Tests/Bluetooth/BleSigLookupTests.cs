using FluentAssertions;
using Iaet.Android.Bluetooth;

namespace Iaet.Android.Tests.Bluetooth;

public sealed class BleSigLookupTests
{
    [Fact]
    public void LookupService_with_full_uuid_returns_name()
    {
        var name = BleSigLookup.LookupService("0000180d-0000-1000-8000-00805f9b34fb");

        name.Should().Be("Heart Rate");
    }

    [Fact]
    public void LookupService_with_short_uuid_returns_name()
    {
        BleSigLookup.LookupService("180D").Should().Be("Heart Rate");
        BleSigLookup.LookupService("180d").Should().Be("Heart Rate");
    }

    [Fact]
    public void LookupService_with_0x_prefix_returns_name()
    {
        BleSigLookup.LookupService("0x180D").Should().Be("Heart Rate");
    }

    [Fact]
    public void LookupService_with_custom_uuid_returns_null()
    {
        var name = BleSigLookup.LookupService("12345678-abcd-efab-cdef-123456789abc");

        name.Should().BeNull();
    }

    [Fact]
    public void LookupCharacteristic_with_full_uuid_returns_name()
    {
        var name = BleSigLookup.LookupCharacteristic("00002a37-0000-1000-8000-00805f9b34fb");

        name.Should().Be("Heart Rate Measurement");
    }

    [Fact]
    public void LookupCharacteristic_with_short_uuid_returns_name()
    {
        BleSigLookup.LookupCharacteristic("2A37").Should().Be("Heart Rate Measurement");
        BleSigLookup.LookupCharacteristic("2a19").Should().Be("Battery Level");
    }

    [Fact]
    public void LookupCharacteristic_with_unknown_uuid_returns_null()
    {
        BleSigLookup.LookupCharacteristic("FFFF").Should().BeNull();
    }

    [Fact]
    public void IsStandardUuid_returns_true_for_standard_full_uuid()
    {
        BleSigLookup.IsStandardUuid("0000180d-0000-1000-8000-00805f9b34fb").Should().BeTrue();
    }

    [Fact]
    public void IsStandardUuid_returns_true_for_short_hex_uuid()
    {
        BleSigLookup.IsStandardUuid("180D").Should().BeTrue();
    }

    [Fact]
    public void IsStandardUuid_returns_false_for_custom_uuid()
    {
        BleSigLookup.IsStandardUuid("12345678-abcd-efab-cdef-123456789abc").Should().BeFalse();
    }

    [Fact]
    public void IsStandardUuid_is_case_insensitive()
    {
        BleSigLookup.IsStandardUuid("0000180D-0000-1000-8000-00805F9B34FB").Should().BeTrue();
    }

    [Theory]
    [InlineData("180F", "Battery Service")]
    [InlineData("1800", "Generic Access")]
    [InlineData("1826", "Fitness Machine")]
    public void LookupService_covers_various_standard_services(string uuid, string expectedName)
    {
        BleSigLookup.LookupService(uuid).Should().Be(expectedName);
    }

    [Theory]
    [InlineData("2A00", "Device Name")]
    [InlineData("2A19", "Battery Level")]
    [InlineData("2A29", "Manufacturer Name String")]
    public void LookupCharacteristic_covers_various_standard_characteristics(string uuid, string expectedName)
    {
        BleSigLookup.LookupCharacteristic(uuid).Should().Be(expectedName);
    }
}
