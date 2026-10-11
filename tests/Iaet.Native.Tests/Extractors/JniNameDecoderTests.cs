using FluentAssertions;
using Iaet.Native.Extractors;

namespace Iaet.Native.Tests.Extractors;

public sealed class JniNameDecoderTests
{
    [Fact]
    public void Decode_simple_symbol()
    {
        var b = JniNameDecoder.Decode("Java_com_example_ble_NativeBridge_sendCommand");

        b.Should().NotBeNull();
        b!.JavaClass.Should().Be("com.example.ble.NativeBridge");
        b.JavaMethod.Should().Be("sendCommand");
        b.Signature.Should().BeNull();
    }

    [Fact]
    public void Decode_handles_escaped_underscore_and_dollar()
    {
        // Java class com.foo.Outer$Inner, method get_value
        var b = JniNameDecoder.Decode("Java_com_foo_Outer_00024Inner_get_1value");

        b.Should().NotBeNull();
        b!.JavaClass.Should().Be("com.foo.Outer$Inner");
        b.JavaMethod.Should().Be("get_value");
    }

    [Fact]
    public void Decode_splits_overload_signature()
    {
        var b = JniNameDecoder.Decode("Java_com_foo_Bar_write___3BI");

        b.Should().NotBeNull();
        b!.JavaClass.Should().Be("com.foo.Bar");
        b.JavaMethod.Should().Be("write");
        b.Signature.Should().Be("[BI");
    }

    [Theory]
    [InlineData("JNI_OnLoad")]
    [InlineData("Java_")]
    [InlineData("Java_onlyone")]
    [InlineData("SSL_CTX_new")]
    public void Decode_returns_null_for_non_jni_symbols(string symbol)
    {
        JniNameDecoder.Decode(symbol).Should().BeNull();
    }
}
