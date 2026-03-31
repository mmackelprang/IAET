using FluentAssertions;
using Iaet.Capture;
using Iaet.Core.Models;

namespace Iaet.Capture.Tests;

public sealed class AuthHealthMonitorTests
{
    [Fact]
    public void IsAuthFailure_detects_401()
    {
        var request = MakeRequest(401);
        AuthHealthMonitor.IsAuthFailure(request).Should().BeTrue();
    }

    [Fact]
    public void IsAuthFailure_detects_403()
    {
        var request = MakeRequest(403);
        AuthHealthMonitor.IsAuthFailure(request).Should().BeTrue();
    }

    [Fact]
    public void IsAuthFailure_returns_false_for_200()
    {
        var request = MakeRequest(200);
        AuthHealthMonitor.IsAuthFailure(request).Should().BeFalse();
    }

    [Fact]
    public void Monitor_tracks_consecutive_failures()
    {
        var monitor = new AuthHealthMonitor();

        monitor.RecordResponse(MakeRequest(200));
        monitor.IsHealthy.Should().BeTrue();

        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));
        monitor.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void Monitor_resets_on_success()
    {
        var monitor = new AuthHealthMonitor();

        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(200));
        monitor.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void Monitor_fires_event_on_unhealthy()
    {
        var monitor = new AuthHealthMonitor(consecutiveFailureThreshold: 2);
        var fired = false;
        monitor.AuthUnhealthy += (_, _) => fired = true;

        monitor.RecordResponse(MakeRequest(401));
        monitor.RecordResponse(MakeRequest(401));

        fired.Should().BeTrue();
    }

    private static CapturedRequest MakeRequest(int status) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = "GET",
        Url = "https://example.com/api",
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = status,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 100,
    };
}
