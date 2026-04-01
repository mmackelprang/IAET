using FluentAssertions;
using Iaet.Android.Bluetooth;
using Iaet.Core.Models;

namespace Iaet.Android.Tests.Bluetooth;

public sealed class BleDataFlowTracerTests
{
    [Fact]
    public void Trace_with_callback_getValue_and_postValue_returns_high_confidence()
    {
        var java = """
            public class HeartRateCallback extends BluetoothGattCallback {
                private MutableLiveData<Integer> heartRate = new MutableLiveData<>();

                @Override
                public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic) {
                    byte[] value = characteristic.getValue();
                    int hr = value[1] & 0xFF;
                    heartRate.postValue(hr);
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "HeartRateCallback.java");

        results.Should().ContainSingle();
        var flow = results[0];
        flow.Confidence.Should().Be(ConfidenceLevel.High);
        flow.ParsingDescription.Should().NotBeNullOrEmpty();
        flow.UiBinding.Should().Contain("postValue");
        flow.VariableName.Should().Be("heartRate");
    }

    [Fact]
    public void Trace_with_callback_only_returns_low_confidence()
    {
        var java = """
            public class a extends BluetoothGattCallback {
                @Override
                public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic) {
                    // obfuscated, no recognizable parsing
                    b(c);
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "a.java");

        results.Should().ContainSingle();
        results[0].Confidence.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void Trace_with_empty_input_returns_empty_list()
    {
        var results = BleDataFlowTracer.Trace("", "empty.java");

        results.Should().BeEmpty();
    }

    [Fact]
    public void Trace_with_null_input_returns_empty_list()
    {
        var results = BleDataFlowTracer.Trace(null!, "file.java");

        results.Should().BeEmpty();
    }

    [Fact]
    public void Trace_with_obfuscated_code_detects_callback_pattern()
    {
        var java = """
            public class z extends BluetoothGattCallback {
                @Override
                public void onCharacteristicChanged(BluetoothGatt a, BluetoothGattCharacteristic b) {
                    byte[] c = b.getValue();
                    ByteBuffer.wrap(c);
                    this.d = e;
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "z.java");

        results.Should().ContainSingle();
        var flow = results[0];
        flow.Confidence.Should().Be(ConfidenceLevel.High);
        flow.ParsingDescription.Should().Contain("getValue");
    }

    [Fact]
    public void Trace_with_callback_and_setText_returns_medium_or_higher_confidence()
    {
        var java = """
            public class SensorView extends BluetoothGattCallback {
                @Override
                public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic ch) {
                    temperatureView.setText(String.valueOf(temp));
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "SensorView.java");

        results.Should().ContainSingle();
        results[0].Confidence.Should().BeOneOf(ConfidenceLevel.Medium, ConfidenceLevel.High);
        results[0].UiBinding.Should().Contain("setText");
    }

    [Fact]
    public void Trace_infers_heart_rate_from_filename()
    {
        var java = """
            public class HeartRateHandler extends BluetoothGattCallback {
                @Override
                public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic ch) {
                    byte[] value = ch.getValue();
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "HeartRateHandler.java");

        results.Should().ContainSingle();
        results[0].InferredMeaning.Should().Contain("heart rate data");
    }

    [Fact]
    public void Trace_infers_battery_from_filename()
    {
        var java = """
            public class BatteryService extends BluetoothGattCallback {
                @Override
                public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic ch) {
                    byte[] data = ch.getValue();
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "BatteryService.java");

        results.Should().ContainSingle();
        results[0].InferredMeaning.Should().Contain("battery level");
    }

    [Fact]
    public void Trace_with_no_callback_returns_empty()
    {
        var java = """
            public class HttpService {
                public void fetch() {
                    response.body().string();
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "HttpService.java");

        results.Should().BeEmpty();
    }

    [Fact]
    public void Trace_with_ByteBuffer_and_postValue_returns_high_confidence()
    {
        var java = """
            public class SensorCallback extends BluetoothGattCallback {
                private MutableLiveData<Float> sensorData;

                @Override
                public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic ch) {
                    byte[] raw = ch.getValue();
                    float val = ByteBuffer.wrap(raw).getFloat();
                    sensorData.postValue(val);
                }
            }
            """;

        var results = BleDataFlowTracer.Trace(java, "SensorCallback.java");

        results.Should().ContainSingle();
        var flow = results[0];
        flow.Confidence.Should().Be(ConfidenceLevel.High);
        flow.ParsingDescription.Should().Contain("ByteBuffer");
        flow.InferredMeaning.Should().Contain("sensor data");
    }

    [Fact]
    public void TraceFromDirectory_returns_empty_for_nonexistent_directory()
    {
        var results = BleDataFlowTracer.TraceFromDirectory("/nonexistent/path");

        results.Should().BeEmpty();
    }

    [Fact]
    public void TraceFromDirectory_throws_on_null_directory()
    {
        var act = () => BleDataFlowTracer.TraceFromDirectory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TraceFromDirectory_scans_java_files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iaet-ble-flow-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "BleHandler.java"), """
                public class BleHandler extends BluetoothGattCallback {
                    @Override
                    public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic ch) {
                        byte[] v = ch.getValue();
                        this.sensorValue = v[0];
                    }
                }
                """);
            File.WriteAllText(Path.Combine(tempDir, "HttpHandler.java"), """
                public class HttpHandler {
                    public void fetch() { }
                }
                """);

            var results = BleDataFlowTracer.TraceFromDirectory(tempDir);

            results.Should().ContainSingle();
            results[0].CallbackLocation.Should().Contain("BleHandler.java");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
