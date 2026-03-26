using FluentAssertions;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Iaet.Export;
using NSubstitute;

namespace Iaet.Export.Tests;

public class ExportContextTests
{
    // ------------------------------------------------------------------ helpers

    private static CaptureSessionInfo MakeSession(Guid? id = null) =>
        new()
        {
            Id                   = id ?? Guid.NewGuid(),
            Name                 = "Test Session",
            TargetApplication    = "TestApp",
            Profile              = "default",
            StartedAt            = DateTimeOffset.UtcNow,
        };

    private static CapturedRequest MakeRequest(Guid sessionId) =>
        new()
        {
            Id              = Guid.NewGuid(),
            SessionId       = sessionId,
            Timestamp       = DateTimeOffset.UtcNow,
            HttpMethod      = "GET",
            Url             = "https://example.com/api/data",
            RequestHeaders  = [],
            ResponseStatus  = 200,
            ResponseHeaders = [],
            DurationMs      = 50,
        };

    private static EndpointGroup MakeGroup(string method = "GET", string path = "/api/data") =>
        new(
            EndpointSignature.FromRequest(method, path),
            ObservationCount: 1,
            FirstSeen: DateTimeOffset.UtcNow,
            LastSeen: DateTimeOffset.UtcNow);

    private static CapturedStream MakeStream(Guid sessionId) =>
        new()
        {
            Id        = Guid.NewGuid(),
            SessionId = sessionId,
            Protocol  = StreamProtocol.WebSocket,
            Url       = "wss://example.com/ws",
            StartedAt = DateTimeOffset.UtcNow,
            Metadata  = new StreamMetadata(new Dictionary<string, string>()),
        };

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task LoadAsync_PopulatesAllData()
    {
        var sessionId = Guid.NewGuid();
        var session   = MakeSession(sessionId);
        var request   = MakeRequest(sessionId);
        var group     = MakeGroup();
        var stream    = MakeStream(sessionId);

        var catalog = Substitute.For<IEndpointCatalog>();
        catalog.ListSessionsAsync(Arg.Any<CancellationToken>())
               .Returns(new[] { session });
        catalog.GetRequestsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
               .Returns(new[] { request });
        catalog.GetEndpointGroupsAsync(sessionId, Arg.Any<CancellationToken>())
               .Returns(new[] { group });
        catalog.GetResponseBodiesAsync(sessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Array.Empty<string>());

        var streamCatalog = Substitute.For<IStreamCatalog>();
        streamCatalog.GetStreamsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
                     .Returns(new[] { stream });

        var schemaInferrer = Substitute.For<ISchemaInferrer>();

        var context = await ExportContext.LoadAsync(
            sessionId, catalog, streamCatalog, schemaInferrer);

        context.Session.Should().Be(session);
        context.Requests.Should().ContainSingle().Which.Should().Be(request);
        context.EndpointGroups.Should().ContainSingle().Which.Should().Be(group);
        context.Streams.Should().ContainSingle().Which.Should().Be(stream);
    }

    [Fact]
    public async Task LoadAsync_InfersSchemasPerEndpoint()
    {
        var sessionId = Guid.NewGuid();
        var session   = MakeSession(sessionId);
        var group1    = MakeGroup("GET",  "/api/users");
        var group2    = MakeGroup("POST", "/api/orders");

        var bodies1 = new[] { """{"id":1}""" };
        var bodies2 = Array.Empty<string>(); // no bodies → InferAsync must NOT be called for this group

        var catalog = Substitute.For<IEndpointCatalog>();
        catalog.ListSessionsAsync(Arg.Any<CancellationToken>())
               .Returns(new[] { session });
        catalog.GetRequestsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
               .Returns(Array.Empty<CapturedRequest>());
        catalog.GetEndpointGroupsAsync(sessionId, Arg.Any<CancellationToken>())
               .Returns(new[] { group1, group2 });
        catalog.GetResponseBodiesAsync(sessionId, group1.Signature.Normalized, Arg.Any<CancellationToken>())
               .Returns(bodies1);
        catalog.GetResponseBodiesAsync(sessionId, group2.Signature.Normalized, Arg.Any<CancellationToken>())
               .Returns(bodies2);

        var streamCatalog = Substitute.For<IStreamCatalog>();
        streamCatalog.GetStreamsBySessionAsync(sessionId, Arg.Any<CancellationToken>())
                     .Returns(Array.Empty<CapturedStream>());

        var expectedSchema = new SchemaResult(
            JsonSchema: """{"type":"object"}""",
            CSharpRecord: "public sealed record InferredResponse();",
            OpenApiFragment: "type: object",
            Warnings: []);

        var schemaInferrer = Substitute.For<ISchemaInferrer>();
        schemaInferrer.InferAsync(bodies1, Arg.Any<CancellationToken>())
                      .Returns(expectedSchema);

        var context = await ExportContext.LoadAsync(
            sessionId, catalog, streamCatalog, schemaInferrer);

        context.SchemasByEndpoint.Should().ContainKey(group1.Signature.Normalized);
        context.SchemasByEndpoint[group1.Signature.Normalized].Should().Be(expectedSchema);
        context.SchemasByEndpoint.Should().NotContainKey(group2.Signature.Normalized);

        await schemaInferrer.Received(1).InferAsync(bodies1, Arg.Any<CancellationToken>());
        await schemaInferrer.DidNotReceive().InferAsync(bodies2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_SessionNotFound_ThrowsDescriptive()
    {
        var sessionId = Guid.NewGuid();

        var catalog = Substitute.For<IEndpointCatalog>();
        catalog.ListSessionsAsync(Arg.Any<CancellationToken>())
               .Returns(Array.Empty<CaptureSessionInfo>());

        var streamCatalog  = Substitute.For<IStreamCatalog>();
        var schemaInferrer = Substitute.For<ISchemaInferrer>();

        var act = async () => await ExportContext.LoadAsync(
            sessionId, catalog, streamCatalog, schemaInferrer);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage($"*{sessionId}*");
    }
}
