using FluentAssertions;
using Iaet.Capture;
using Iaet.Core.Abstractions;
using NSubstitute;

namespace Iaet.Capture.Tests;

public sealed class CaptureSessionStreamIntegrationTests
{
    [Fact]
    public void Factory_Create_WithoutListeners_CreatesSession()
    {
        var factory = new PlaywrightCaptureSessionFactory();
        var options = new CaptureOptions
        {
            TargetApplication = "TestApp",
            Headless = true
        };

        var session = factory.Create(options);

        session.Should().NotBeNull();
        session.TargetApplication.Should().Be("TestApp");
    }

    [Fact]
    public void Factory_Create_WithNullListeners_CreatesSession()
    {
        var factory = new PlaywrightCaptureSessionFactory();
        var options = new CaptureOptions
        {
            TargetApplication = "TestApp",
            Headless = true
        };
        var catalog = Substitute.For<IStreamCatalog>();

        var session = factory.Create(options, catalog, null);

        session.Should().NotBeNull();
        session.TargetApplication.Should().Be("TestApp");
    }

    [Fact]
    public void Factory_Create_WithListeners_CreatesSession()
    {
        var factory = new PlaywrightCaptureSessionFactory();
        var options = new CaptureOptions
        {
            TargetApplication = "TestApp",
            Headless = true,
            Streams = new StreamCaptureOptions { Enabled = true }
        };
        var catalog = Substitute.For<IStreamCatalog>();
        var listener = Substitute.For<IProtocolListener>();

        var session = factory.Create(options, catalog, [listener]);

        session.Should().NotBeNull();
        session.TargetApplication.Should().Be("TestApp");
    }

    [Fact]
    public void Factory_Create_BackwardCompat_NoStreamArgs()
    {
        // The original 1-arg overload still works without any stream wiring.
        var factory = new PlaywrightCaptureSessionFactory();
        var options = new CaptureOptions
        {
            TargetApplication = "LegacyApp",
            Headless = true
        };

        var session = factory.Create(options);

        session.Should().BeOfType<PlaywrightCaptureSession>();
        session.IsRecording.Should().BeFalse();
    }
}
