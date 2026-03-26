using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Export;

/// <summary>
/// Aggregates all session data — requests, endpoint groups, streams, and inferred schemas —
/// into a single context object for use by export generators.
/// </summary>
public sealed class ExportContext
{
    /// <summary>Gets the capture session metadata.</summary>
    public required CaptureSessionInfo Session { get; init; }

    /// <summary>Gets all HTTP requests captured during the session.</summary>
    public required IReadOnlyList<CapturedRequest> Requests { get; init; }

    /// <summary>Gets the endpoint groups (deduplicated, normalised) for the session.</summary>
    public required IReadOnlyList<EndpointGroup> EndpointGroups { get; init; }

    /// <summary>Gets all WebSocket / SSE / other streams captured during the session.</summary>
    public required IReadOnlyList<CapturedStream> Streams { get; init; }

    /// <summary>
    /// Gets inferred JSON schemas keyed by <see cref="EndpointSignature.Normalized"/>.
    /// Only endpoints that had at least one recorded response body are included.
    /// </summary>
    public required IReadOnlyDictionary<string, SchemaResult> SchemasByEndpoint { get; init; }

    /// <summary>
    /// Loads all data for the given <paramref name="sessionId"/> and infers schemas for
    /// every endpoint that has recorded response bodies.
    /// </summary>
    /// <param name="sessionId">The session to load.</param>
    /// <param name="catalog">Source of HTTP request / endpoint data.</param>
    /// <param name="streamCatalog">Source of stream data.</param>
    /// <param name="schemaInferrer">Infers JSON Schema from a list of response bodies.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A fully-populated <see cref="ExportContext"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="sessionId"/> does not match any session in the catalog.
    /// </exception>
    public static async Task<ExportContext> LoadAsync(
        Guid sessionId,
        IEndpointCatalog catalog,
        IStreamCatalog streamCatalog,
        ISchemaInferrer schemaInferrer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(streamCatalog);
        ArgumentNullException.ThrowIfNull(schemaInferrer);

        var sessions = await catalog.ListSessionsAsync(ct).ConfigureAwait(false);
        var session  = sessions.FirstOrDefault(s => s.Id == sessionId)
                       ?? throw new InvalidOperationException(
                           $"Session {sessionId} not found in catalog");

        var requests = await catalog.GetRequestsBySessionAsync(sessionId, ct).ConfigureAwait(false);
        var groups   = await catalog.GetEndpointGroupsAsync(sessionId, ct).ConfigureAwait(false);
        var streams  = await streamCatalog.GetStreamsBySessionAsync(sessionId, ct).ConfigureAwait(false);

        var schemas = new Dictionary<string, SchemaResult>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var bodies = await catalog.GetResponseBodiesAsync(
                sessionId, group.Signature.Normalized, ct).ConfigureAwait(false);

            if (bodies.Count > 0)
            {
                var schema = await schemaInferrer.InferAsync(bodies, ct).ConfigureAwait(false);
                schemas[group.Signature.Normalized] = schema;
            }
        }

        return new ExportContext
        {
            Session          = session,
            Requests         = requests,
            EndpointGroups   = groups,
            Streams          = streams,
            SchemasByEndpoint = schemas,
        };
    }
}
