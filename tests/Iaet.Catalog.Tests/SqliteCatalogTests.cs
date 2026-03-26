using FluentAssertions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog.Tests;

public sealed class SqliteCatalogTests : IDisposable
{
    private readonly CatalogDbContext _db;
    private readonly SqliteCatalog _catalog;

    public SqliteCatalogTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CatalogDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _catalog = new SqliteCatalog(_db);
    }

    [Fact]
    public async Task SaveAndListSession_RoundTrips()
    {
        var session = new CaptureSessionInfo
        {
            Id = Guid.NewGuid(),
            Name = "test-session",
            TargetApplication = "TestApp",
            Profile = "test-profile",
            StartedAt = DateTimeOffset.UtcNow
        };

        await _catalog.SaveSessionAsync(session);
        var sessions = await _catalog.ListSessionsAsync();
        sessions.Should().ContainSingle().Which.Name.Should().Be("test-session");
    }

    [Fact]
    public async Task SaveRequest_GroupsBySignature()
    {
        var sessionId = Guid.NewGuid();
        var session = new CaptureSessionInfo
        {
            Id = sessionId, Name = "s1", TargetApplication = "App",
            Profile = "p1", StartedAt = DateTimeOffset.UtcNow
        };
        await _catalog.SaveSessionAsync(session);

        await _catalog.SaveRequestAsync(MakeRequest(sessionId, "GET", "https://api.test/users/123"));
        await _catalog.SaveRequestAsync(MakeRequest(sessionId, "GET", "https://api.test/users/456"));

        var groups = await _catalog.GetEndpointGroupsAsync(sessionId);
        groups.Should().ContainSingle()
            .Which.ObservationCount.Should().Be(2);
    }

    [Fact]
    public async Task GetResponseBodiesAsync_ReturnsNonNullBodies()
    {
        var sessionId = Guid.NewGuid();
        var session = new CaptureSessionInfo
        {
            Id = sessionId, Name = "s-bodies", TargetApplication = "App",
            Profile = "p1", StartedAt = DateTimeOffset.UtcNow
        };
        await _catalog.SaveSessionAsync(session);

        var req1 = MakeRequest(sessionId, "GET", "https://api.test/items/1") with { ResponseBody = "body-one" };
        var req2 = MakeRequest(sessionId, "GET", "https://api.test/items/2") with { ResponseBody = "body-two" };
        await _catalog.SaveRequestAsync(req1);
        await _catalog.SaveRequestAsync(req2);

        // Both requests normalize to the same signature: GET https://api.test/items/{id}
        var groups = await _catalog.GetEndpointGroupsAsync(sessionId);
        var sig = groups.Should().ContainSingle().Subject.Signature.Normalized;

        var bodies = await _catalog.GetResponseBodiesAsync(sessionId, sig);
        bodies.Should().HaveCount(2).And.Contain("body-one").And.Contain("body-two");
    }

    [Fact]
    public async Task GetResponseBodiesAsync_ExcludesNullBodies()
    {
        var sessionId = Guid.NewGuid();
        var session = new CaptureSessionInfo
        {
            Id = sessionId, Name = "s-null-body", TargetApplication = "App",
            Profile = "p1", StartedAt = DateTimeOffset.UtcNow
        };
        await _catalog.SaveSessionAsync(session);

        var req1 = MakeRequest(sessionId, "GET", "https://api.test/items/1") with { ResponseBody = "has-body" };
        var req2 = MakeRequest(sessionId, "GET", "https://api.test/items/2") with { ResponseBody = null };
        await _catalog.SaveRequestAsync(req1);
        await _catalog.SaveRequestAsync(req2);

        var groups = await _catalog.GetEndpointGroupsAsync(sessionId);
        var sig = groups.Should().ContainSingle().Subject.Signature.Normalized;

        var bodies = await _catalog.GetResponseBodiesAsync(sessionId, sig);
        bodies.Should().ContainSingle().Which.Should().Be("has-body");
    }

    [Fact]
    public async Task GetRequestByIdAsync_ReturnsMatchingRequest()
    {
        var sessionId = Guid.NewGuid();
        var session = new CaptureSessionInfo
        {
            Id = sessionId, Name = "s-byid", TargetApplication = "App",
            Profile = "p1", StartedAt = DateTimeOffset.UtcNow
        };
        await _catalog.SaveSessionAsync(session);

        var req = MakeRequest(sessionId, "POST", "https://api.test/orders");
        await _catalog.SaveRequestAsync(req);

        var result = await _catalog.GetRequestByIdAsync(req.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(req.Id);
        result.HttpMethod.Should().Be("POST");
        result.Url.Should().Be("https://api.test/orders");
    }

    [Fact]
    public async Task GetRequestByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _catalog.GetRequestByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    private static CapturedRequest MakeRequest(Guid sessionId, string method, string url) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 50
    };

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
