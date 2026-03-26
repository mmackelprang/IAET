using FluentAssertions;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;

namespace Iaet.Explorer.Tests;

/// <summary>
/// Integration tests for the Explorer minimal API endpoints using a custom in-memory host.
/// </summary>
public sealed class ExplorerApiTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;

    internal static readonly Guid SessionId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private static readonly CaptureSessionInfo TestSession = new()
    {
        Id = SessionId,
        Name = "Test Session",
        TargetApplication = "TestApp",
        Profile = "default",
        StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
        CapturedRequestCount = 0
    };

    public async Task InitializeAsync()
    {
        var catalog = Substitute.For<IEndpointCatalog>();
        var streamCatalog = Substitute.For<IStreamCatalog>();
        var schemaInferrer = Substitute.For<ISchemaInferrer>();
        var replayEngine = Substitute.For<IReplayEngine>();

        catalog.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CaptureSessionInfo> { TestSession });
        catalog.GetEndpointGroupsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<EndpointGroup>());
        catalog.GetRequestsBySessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<CapturedRequest>());
        streamCatalog.GetStreamsBySessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<CapturedStream>());

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRazorPages();
        builder.Services.AddSingleton(catalog);
        builder.Services.AddSingleton(streamCatalog);
        builder.Services.AddSingleton(schemaInferrer);
        builder.Services.AddSingleton(replayEngine);

        _app = builder.Build();
        _app.UseStaticFiles();
        _app.UseRouting();
        _app.MapRazorPages();

        Api.SessionsApi.Map(_app);
        Api.EndpointsApi.Map(_app);
        Api.StreamsApi.Map(_app);
        Api.SchemaApi.Map(_app);
        Api.ReplayApi.Map(_app);
        Api.ExportApi.Map(_app);

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetSessions_Returns200()
    {
        var response = await _client!.GetAsync(new Uri("/api/sessions", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSessions_ReturnsSessionList()
    {
        var response = await _client!.GetAsync(new Uri("/api/sessions", UriKind.Relative));
        var sessions = await response.Content.ReadFromJsonAsync<List<CaptureSessionInfo>>();
        sessions.Should().NotBeNull();
        sessions!.Should().HaveCount(1);
        sessions[0].Name.Should().Be("Test Session");
    }

    [Fact]
    public async Task GetSession_WithValidId_Returns200()
    {
        var response = await _client!.GetAsync(new Uri($"/api/sessions/{SessionId}", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSession_WithUnknownId_Returns404()
    {
        var response = await _client!.GetAsync(new Uri($"/api/sessions/{Guid.NewGuid()}", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEndpoints_WithValidSession_Returns200()
    {
        var response = await _client!.GetAsync(new Uri($"/api/sessions/{SessionId}/endpoints", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStreams_WithValidSession_Returns200()
    {
        var response = await _client!.GetAsync(new Uri($"/api/sessions/{SessionId}/streams", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
