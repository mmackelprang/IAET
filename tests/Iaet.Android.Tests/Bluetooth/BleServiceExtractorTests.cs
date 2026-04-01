using FluentAssertions;
using Iaet.Android.Bluetooth;
using Iaet.Core.Models;

namespace Iaet.Android.Tests.Bluetooth;

public sealed class BleServiceExtractorTests
{
    [Fact]
    public void Extract_finds_UuidFromString_calls()
    {
        var java = """
            public class HeartRateService {
                private static final UUID HR_SERVICE_UUID =
                    UUID.fromString("0000180d-0000-1000-8000-00805f9b34fb");
                private static final UUID HR_MEASUREMENT_UUID =
                    UUID.fromString("00002a37-0000-1000-8000-00805f9b34fb");
            }
            """;

        var result = BleServiceExtractor.Extract(java, "HeartRateService.java");

        result.Services.Should().Contain(s => s.Uuid == "0000180d-0000-1000-8000-00805f9b34fb");
        result.Characteristics.Should().Contain(c => c.Uuid == "00002a37-0000-1000-8000-00805f9b34fb");
    }

    [Fact]
    public void Extract_finds_readCharacteristic_operations()
    {
        var java = """
            public class BleReader {
                private static final UUID CHAR_UUID =
                    UUID.fromString("00002a19-0000-1000-8000-00805f9b34fb");

                public void read(BluetoothGatt gatt) {
                    BluetoothGattCharacteristic c = service.getCharacteristic(CHAR_UUID);
                    gatt.readCharacteristic(c);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "BleReader.java");

        result.Characteristics.Should().Contain(c =>
            c.Uuid == "00002a19-0000-1000-8000-00805f9b34fb"
            && c.Operations.Contains(BleOperationType.Read));
    }

    [Fact]
    public void Extract_finds_writeCharacteristic_operations()
    {
        var java = """
            public class BleWriter {
                private static final UUID CONTROL_POINT_UUID =
                    UUID.fromString("00002a39-0000-1000-8000-00805f9b34fb");

                public void write(BluetoothGatt gatt, byte[] data) {
                    BluetoothGattCharacteristic c = service.getCharacteristic(CONTROL_POINT_UUID);
                    c.setValue(data);
                    gatt.writeCharacteristic(c);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "BleWriter.java");

        result.Characteristics.Should().Contain(c =>
            c.Uuid == "00002a39-0000-1000-8000-00805f9b34fb"
            && c.Operations.Contains(BleOperationType.Write));
    }

    [Fact]
    public void Extract_finds_setCharacteristicNotification_as_notify()
    {
        var java = """
            public class BleNotifier {
                private static final UUID HR_MEASUREMENT_UUID =
                    UUID.fromString("00002a37-0000-1000-8000-00805f9b34fb");

                public void enableNotify(BluetoothGatt gatt) {
                    BluetoothGattCharacteristic c = service.getCharacteristic(HR_MEASUREMENT_UUID);
                    gatt.setCharacteristicNotification(c, true);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "BleNotifier.java");

        result.Characteristics.Should().Contain(c =>
            c.Uuid == "00002a37-0000-1000-8000-00805f9b34fb"
            && c.Operations.Contains(BleOperationType.Notify));
    }

    [Fact]
    public void Extract_associates_operations_with_characteristics_by_proximity()
    {
        var java = """
            public class MultiCharService {
                private static final UUID CHAR_A =
                    UUID.fromString("00002a19-0000-1000-8000-00805f9b34fb");

                public void doRead(BluetoothGatt gatt) {
                    gatt.readCharacteristic(charA);
                }

                private static final UUID CHAR_B =
                    UUID.fromString("00002a37-0000-1000-8000-00805f9b34fb");

                public void doNotify(BluetoothGatt gatt) {
                    gatt.setCharacteristicNotification(charB, true);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "MultiCharService.java");

        // CHAR_A should have Read (closest UUID to readCharacteristic)
        var charA = result.Characteristics.FirstOrDefault(c => c.Uuid == "00002a19-0000-1000-8000-00805f9b34fb");
        charA.Should().NotBeNull();
        charA!.Operations.Should().Contain(BleOperationType.Read);

        // CHAR_B should have Notify (closest UUID to setCharacteristicNotification)
        var charB = result.Characteristics.FirstOrDefault(c => c.Uuid == "00002a37-0000-1000-8000-00805f9b34fb");
        charB.Should().NotBeNull();
        charB!.Operations.Should().Contain(BleOperationType.Notify);
    }

    [Fact]
    public void Extract_works_with_obfuscated_class_names()
    {
        var java = """
            public class a {
                private static final UUID b =
                    UUID.fromString("0000180f-0000-1000-8000-00805f9b34fb");
                private static final UUID c =
                    UUID.fromString("00002a19-0000-1000-8000-00805f9b34fb");

                public void d(BluetoothGatt e) {
                    BluetoothGattService f = e.getService(b);
                    BluetoothGattCharacteristic g = f.getCharacteristic(c);
                    e.readCharacteristic(g);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "a.java");

        result.Services.Should().Contain(s => s.Uuid == "0000180f-0000-1000-8000-00805f9b34fb");
        result.Characteristics.Should().Contain(c =>
            c.Uuid == "00002a19-0000-1000-8000-00805f9b34fb"
            && c.Operations.Contains(BleOperationType.Read));
    }

    [Fact]
    public void Extract_resolves_standard_names_via_BleSigLookup()
    {
        var java = """
            public class HrService {
                private static final UUID SERVICE_UUID =
                    UUID.fromString("0000180d-0000-1000-8000-00805f9b34fb");
                private static final UUID MEASUREMENT_UUID =
                    UUID.fromString("00002a37-0000-1000-8000-00805f9b34fb");
            }
            """;

        var result = BleServiceExtractor.Extract(java, "HrService.java");

        var service = result.Services.FirstOrDefault(s => s.Uuid == "0000180d-0000-1000-8000-00805f9b34fb");
        service.Should().NotBeNull();
        service!.Name.Should().Be("Heart Rate");
        service.IsStandardService.Should().BeTrue();

        var characteristic = result.Characteristics.FirstOrDefault(c => c.Uuid == "00002a37-0000-1000-8000-00805f9b34fb");
        characteristic.Should().NotBeNull();
        characteristic!.Name.Should().Be("Heart Rate Measurement");
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        var result = BleServiceExtractor.Extract("", "empty.java");

        result.Services.Should().BeEmpty();
        result.Characteristics.Should().BeEmpty();
    }

    [Fact]
    public void Extract_handles_null_source_returns_empty()
    {
        var result = BleServiceExtractor.Extract(null!, "file.java");

        result.Services.Should().BeEmpty();
        result.Characteristics.Should().BeEmpty();
    }

    [Fact]
    public void Extract_finds_custom_vendor_uuids()
    {
        var java = """
            public class VendorBleService {
                private static final UUID VENDOR_CHAR =
                    UUID.fromString("12345678-abcd-efab-cdef-123456789abc");

                public void sendCommand(BluetoothGatt gatt) {
                    BluetoothGattCharacteristic c = service.getCharacteristic(VENDOR_CHAR);
                    gatt.writeCharacteristic(c);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "VendorBleService.java");

        var vendorChar = result.Characteristics.FirstOrDefault(c => c.Uuid == "12345678-abcd-efab-cdef-123456789abc");
        vendorChar.Should().NotBeNull();
        vendorChar!.Name.Should().BeNull(); // Not a standard UUID
        vendorChar.Operations.Should().Contain(BleOperationType.Write);
    }

    [Fact]
    public void Extract_detects_indicate_operation()
    {
        var java = """
            public class IndicateService {
                private static final UUID CHAR_UUID =
                    UUID.fromString("00002a1c-0000-1000-8000-00805f9b34fb");

                public void enableIndication(BluetoothGatt gatt) {
                    BluetoothGattCharacteristic c = service.getCharacteristic(CHAR_UUID);
                    gatt.setCharacteristicNotification(c, true);
                    descriptor.setValue(BluetoothGattDescriptor.ENABLE_INDICATION);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "IndicateService.java");

        result.Characteristics.Should().Contain(c =>
            c.Uuid == "00002a1c-0000-1000-8000-00805f9b34fb"
            && c.Operations.Contains(BleOperationType.Indicate));
    }

    [Fact]
    public void Extract_sets_high_confidence_for_standard_services()
    {
        var java = """
            public class StandardService {
                private static final UUID SERVICE =
                    UUID.fromString("0000180d-0000-1000-8000-00805f9b34fb");
                BluetoothGattService svc = gatt.getService(SERVICE);
            }
            """;

        var result = BleServiceExtractor.Extract(java, "StandardService.java");

        result.Services.Should().Contain(s =>
            s.Uuid == "0000180d-0000-1000-8000-00805f9b34fb"
            && s.Confidence == ConfidenceLevel.High);
    }

    [Fact]
    public void ExtractFromDirectory_scans_all_java_files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iaet-ble-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Service.java"), """
                public class Service {
                    private static final UUID HR_SERVICE =
                        UUID.fromString("0000180d-0000-1000-8000-00805f9b34fb");
                    BluetoothGattService svc = gatt.getService(HR_SERVICE);
                }
                """);
            File.WriteAllText(Path.Combine(tempDir, "Reader.java"), """
                public class Reader {
                    private static final UUID BATTERY_LEVEL =
                        UUID.fromString("00002a19-0000-1000-8000-00805f9b34fb");
                    gatt.readCharacteristic(c);
                }
                """);
            File.WriteAllText(Path.Combine(tempDir, "NotBle.java"), """
                public class NotBle {
                    String url = "https://example.com";
                }
                """);

            var result = BleServiceExtractor.ExtractFromDirectory(tempDir);

            result.Services.Should().Contain(s => s.Uuid == "0000180d-0000-1000-8000-00805f9b34fb");
            result.Characteristics.Should().Contain(c => c.Uuid == "00002a19-0000-1000-8000-00805f9b34fb");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ExtractFromDirectory_returns_empty_for_nonexistent_directory()
    {
        var result = BleServiceExtractor.ExtractFromDirectory("/nonexistent/path");

        result.Services.Should().BeEmpty();
        result.Characteristics.Should().BeEmpty();
    }

    [Fact]
    public void Extract_excludes_AudioEffect_uuid_from_ble_results()
    {
        // AcousticEchoCanceler.EFFECT_TYPE is a UUID near an AudioEffect class reference
        var java = """
            import android.media.audiofx.AcousticEchoCanceler;
            public class AudioManager {
                private static final UUID AEC_UUID =
                    UUID.fromString("bb392ec0-8d2d-11e0-a896-0002a5d5c51b");
                if (AcousticEchoCanceler.isAvailable()) {
                    AcousticEchoCanceler aec = AcousticEchoCanceler.create(sessionId);
                }
            }
            """;

        var result = BleServiceExtractor.Extract(java, "AudioManager.java");

        result.AudioEffectUuids.Should().Contain("bb392ec0-8d2d-11e0-a896-0002a5d5c51b");
        result.Services.Should().NotContain(s => s.Uuid == "bb392ec0-8d2d-11e0-a896-0002a5d5c51b");
        result.Characteristics.Should().NotContain(c => c.Uuid == "bb392ec0-8d2d-11e0-a896-0002a5d5c51b");
    }

    [Fact]
    public void Extract_keeps_uuid_near_BluetoothGattService_in_ble_results()
    {
        var java = """
            public class HeartRateManager {
                private static final UUID SERVICE_UUID =
                    UUID.fromString("0000180d-0000-1000-8000-00805f9b34fb");
                BluetoothGattService service = gatt.getService(SERVICE_UUID);
            }
            """;

        var result = BleServiceExtractor.Extract(java, "HeartRateManager.java");

        result.Services.Should().Contain(s => s.Uuid == "0000180d-0000-1000-8000-00805f9b34fb");
        result.AudioEffectUuids.Should().NotContain("0000180d-0000-1000-8000-00805f9b34fb");
    }

    [Fact]
    public void Extract_keeps_uuid_with_no_context_as_ble_characteristic()
    {
        // Conservative: if there's no AudioEffect context, assume it is BLE
        var java = """
            public class UnknownService {
                private static final UUID SOME_UUID =
                    UUID.fromString("12345678-abcd-ef01-2345-6789abcdef01");
            }
            """;

        var result = BleServiceExtractor.Extract(java, "UnknownService.java");

        result.Characteristics.Should().Contain(c => c.Uuid == "12345678-abcd-ef01-2345-6789abcdef01");
        result.AudioEffectUuids.Should().NotContain("12345678-abcd-ef01-2345-6789abcdef01");
    }
}
