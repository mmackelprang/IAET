using Iaet.Core.Models;
using Iaet.Export;

namespace Iaet.Export.Tests;

/// <summary>
/// Shared factory for building <see cref="ExportContext"/> instances in generator tests.
/// </summary>
internal static class TestContextFactory
{
    private static readonly Guid SessionId = new("11111111-0000-0000-0000-000000000001");

    /// <summary>
    /// Returns an <see cref="ExportContext"/> with 1 session, 2 requests
    /// (GET /api/users/123 and POST /api/data), 1 endpoint group, schema results,
    /// and request/response headers including a redacted auth header.
    /// </summary>
    public static ExportContext MakeContext()
    {
        var session = new CaptureSessionInfo
        {
            Id                   = SessionId,
            Name                 = "Test Session",
            TargetApplication    = "TestApp",
            Profile              = "default",
            StartedAt            = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero),
            CapturedRequestCount = 2,
        };

        var requestGet = new CapturedRequest
        {
            Id              = new Guid("22222222-0000-0000-0000-000000000001"),
            SessionId       = SessionId,
            Timestamp       = new DateTimeOffset(2025, 6, 1, 12, 0, 1, TimeSpan.Zero),
            HttpMethod      = "GET",
            Url             = "https://testapp.example.com/api/users/123",
            RequestHeaders  = new Dictionary<string, string>
            {
                ["Authorization"] = "<REDACTED>",
                ["Accept"]        = "application/json",
            },
            RequestBody     = null,
            ResponseStatus  = 200,
            ResponseHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
            },
            ResponseBody    = """{"id":123,"name":"Alice"}""",
            DurationMs      = 42,
            Tag             = null,
        };

        var requestPost = new CapturedRequest
        {
            Id              = new Guid("22222222-0000-0000-0000-000000000002"),
            SessionId       = SessionId,
            Timestamp       = new DateTimeOffset(2025, 6, 1, 12, 0, 2, TimeSpan.Zero),
            HttpMethod      = "POST",
            Url             = "https://testapp.example.com/api/data",
            RequestHeaders  = new Dictionary<string, string>
            {
                ["Authorization"] = "<REDACTED>",
                ["Content-Type"]  = "application/json",
            },
            RequestBody     = """{"value":"test"}""",
            ResponseStatus  = 201,
            ResponseHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
            },
            ResponseBody    = """{"id":1,"value":"test"}""",
            DurationMs      = 75,
            Tag             = null,
        };

        var endpointGroup = new EndpointGroup(
            EndpointSignature.FromRequest("GET", "/api/users/{id}"),
            ObservationCount: 1,
            FirstSeen: new DateTimeOffset(2025, 6, 1, 12, 0, 1, TimeSpan.Zero),
            LastSeen: new DateTimeOffset(2025, 6, 1, 12, 0, 1, TimeSpan.Zero));

        var schemaResult = new SchemaResult(
            JsonSchema: """{"type":"object","properties":{"id":{"type":"integer"},"name":{"type":"string"}}}""",
            CSharpRecord: "public sealed record UsersResponse(int Id, string Name);",
            OpenApiFragment: "type: object\nproperties:\n  id:\n    type: integer\n  name:\n    type: string",
            Warnings: []);

        return new ExportContext
        {
            Session        = session,
            Requests       = [requestGet, requestPost],
            EndpointGroups = [endpointGroup],
            Streams        = [],
            SchemasByEndpoint = new Dictionary<string, SchemaResult>
            {
                [endpointGroup.Signature.Normalized] = schemaResult,
            },
        };
    }

    /// <summary>
    /// Returns an <see cref="ExportContext"/> identical to <see cref="MakeContext"/> but
    /// with one additional WebSocket stream.
    /// </summary>
    public static ExportContext MakeContextWithStreams()
    {
        var ctx = MakeContext();

        var stream = new CapturedStream
        {
            Id        = new Guid("33333333-0000-0000-0000-000000000001"),
            SessionId = SessionId,
            Protocol  = StreamProtocol.WebSocket,
            Url       = "wss://testapp.example.com/ws/events",
            StartedAt = new DateTimeOffset(2025, 6, 1, 12, 0, 5, TimeSpan.Zero),
            Metadata  = new StreamMetadata(new Dictionary<string, string>
            {
                ["subprotocol"] = "chat-v1",
            }),
        };

        return new ExportContext
        {
            Session           = ctx.Session,
            Requests          = ctx.Requests,
            EndpointGroups    = ctx.EndpointGroups,
            Streams           = [stream],
            SchemasByEndpoint = ctx.SchemasByEndpoint,
        };
    }
}
