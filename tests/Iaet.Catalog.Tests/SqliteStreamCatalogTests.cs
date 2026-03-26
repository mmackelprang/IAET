using FluentAssertions;
using Iaet.Catalog.Entities;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog.Tests;

public sealed class SqliteStreamCatalogTests : IDisposable
{
    private readonly CatalogDbContext _db;
    private readonly SqliteStreamCatalog _catalog;

    public SqliteStreamCatalogTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CatalogDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _catalog = new SqliteStreamCatalog(_db);
    }

    [Fact]
    public async Task SaveAndGet_RoundTrips()
    {
        var sessionId = Guid.NewGuid();
        await SeedSessionAsync(sessionId);

        var stream = MakeStream(sessionId, StreamProtocol.WebSocket, "wss://example.com/ws");

        await _catalog.SaveStreamAsync(stream);
        var results = await _catalog.GetStreamsBySessionAsync(sessionId);

        results.Should().ContainSingle();
        var retrieved = results[0];
        retrieved.Id.Should().Be(stream.Id);
        retrieved.SessionId.Should().Be(sessionId);
        retrieved.Protocol.Should().Be(StreamProtocol.WebSocket);
        retrieved.Url.Should().Be("wss://example.com/ws");
        retrieved.StartedAt.Should().BeCloseTo(stream.StartedAt, TimeSpan.FromSeconds(1));
        retrieved.Metadata.Properties.Should().ContainKey("key1")
            .WhoseValue.Should().Be("value1");
        retrieved.Tag.Should().Be("test-tag");
    }

    [Fact]
    public async Task GetStreamsBySession_ReturnsOnlyMatchingSession()
    {
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        await SeedSessionAsync(sessionA);
        await SeedSessionAsync(sessionB);

        await _catalog.SaveStreamAsync(MakeStream(sessionA, StreamProtocol.WebSocket, "wss://a.com/ws"));
        await _catalog.SaveStreamAsync(MakeStream(sessionB, StreamProtocol.ServerSentEvents, "https://b.com/sse"));

        var resultsA = await _catalog.GetStreamsBySessionAsync(sessionA);
        resultsA.Should().ContainSingle()
            .Which.Protocol.Should().Be(StreamProtocol.WebSocket);

        var resultsB = await _catalog.GetStreamsBySessionAsync(sessionB);
        resultsB.Should().ContainSingle()
            .Which.Protocol.Should().Be(StreamProtocol.ServerSentEvents);

        var resultsC = await _catalog.GetStreamsBySessionAsync(Guid.NewGuid());
        resultsC.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveStream_WithFrames_PersistsFrameData()
    {
        var sessionId = Guid.NewGuid();
        await SeedSessionAsync(sessionId);

        var stream = MakeStream(sessionId, StreamProtocol.WebSocket, "wss://example.com/ws",
            frames:
            [
                new StreamFrame
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Direction = StreamFrameDirection.Sent,
                    TextPayload = "hello",
                    SizeBytes = 5
                },
                new StreamFrame
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Direction = StreamFrameDirection.Received,
                    TextPayload = "world",
                    SizeBytes = 5
                }
            ]);

        await _catalog.SaveStreamAsync(stream);
        var results = await _catalog.GetStreamsBySessionAsync(sessionId);

        results.Should().ContainSingle();
        var retrieved = results[0];
        retrieved.Frames.Should().HaveCount(2);
        retrieved.Frames![0].TextPayload.Should().Be("hello");
        retrieved.Frames![0].Direction.Should().Be(StreamFrameDirection.Sent);
        retrieved.Frames![1].TextPayload.Should().Be("world");
        retrieved.Frames![1].Direction.Should().Be(StreamFrameDirection.Received);
    }

    [Fact]
    public async Task GetStreamByIdAsync_ReturnsCorrectStream()
    {
        var sessionId = Guid.NewGuid();
        await SeedSessionAsync(sessionId);

        var stream1 = MakeStream(sessionId, StreamProtocol.WebSocket, "wss://a.com/ws");
        var stream2 = MakeStream(sessionId, StreamProtocol.ServerSentEvents, "https://a.com/sse");

        await _catalog.SaveStreamAsync(stream1);
        await _catalog.SaveStreamAsync(stream2);

        var result = await _catalog.GetStreamByIdAsync(stream1.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(stream1.Id);
        result.Protocol.Should().Be(StreamProtocol.WebSocket);

        var notFound = await _catalog.GetStreamByIdAsync(Guid.NewGuid());
        notFound.Should().BeNull();
    }

    private async Task SeedSessionAsync(Guid sessionId)
    {
        _db.Sessions.Add(new CaptureSessionEntity
        {
            Id = sessionId,
            Name = $"session-{sessionId}",
            TargetApplication = "TestApp",
            Profile = "default",
            StartedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static CapturedStream MakeStream(
        Guid sessionId,
        StreamProtocol protocol,
        string url,
        IReadOnlyList<StreamFrame>? frames = null) => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Protocol = protocol,
            Url = url,
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = new StreamMetadata(new Dictionary<string, string>
            {
                ["key1"] = "value1"
            }),
            Frames = frames,
            Tag = "test-tag"
        };

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
