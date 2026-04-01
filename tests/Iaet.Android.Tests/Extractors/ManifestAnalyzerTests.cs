using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class ManifestAnalyzerTests
{
    private const string SampleManifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <manifest xmlns:android="http://schemas.android.com/apk/res/android"
            package="com.example.myapp"
            android:versionCode="100"
            android:versionName="1.2.3">
            <uses-sdk android:minSdkVersion="24" android:targetSdkVersion="34" />
            <uses-permission android:name="android.permission.INTERNET" />
            <uses-permission android:name="android.permission.BLUETOOTH" />
            <uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />
            <application>
                <service android:name=".SipService" android:exported="true" />
                <service android:name=".InternalService" android:exported="false" />
                <receiver android:name=".BootReceiver" android:exported="true">
                    <intent-filter>
                        <action android:name="android.intent.action.BOOT_COMPLETED" />
                    </intent-filter>
                </receiver>
            </application>
        </manifest>
        """;

    [Fact]
    public void Parse_extracts_package_info()
    {
        var info = ManifestAnalyzer.Parse(SampleManifest);

        info.PackageName.Should().Be("com.example.myapp");
        info.VersionName.Should().Be("1.2.3");
        info.VersionCode.Should().Be(100);
        info.MinSdk.Should().Be(24);
        info.TargetSdk.Should().Be(34);
    }

    [Fact]
    public void Parse_extracts_permissions()
    {
        var info = ManifestAnalyzer.Parse(SampleManifest);

        info.Permissions.Should().Contain("android.permission.INTERNET");
        info.Permissions.Should().Contain("android.permission.BLUETOOTH");
        info.Permissions.Should().Contain("android.permission.BLUETOOTH_CONNECT");
    }

    [Fact]
    public void Parse_extracts_exported_components()
    {
        var info = ManifestAnalyzer.Parse(SampleManifest);

        info.ExportedServices.Should().Contain(".SipService");
        info.ExportedServices.Should().NotContain(".InternalService");
        info.ExportedReceivers.Should().Contain(".BootReceiver");
    }

    [Fact]
    public void Parse_handles_empty()
    {
        var info = ManifestAnalyzer.Parse("");

        info.PackageName.Should().Be("unknown");
        info.Permissions.Should().BeEmpty();
    }
}
