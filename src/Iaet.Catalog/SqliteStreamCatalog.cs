using System.Text.Json;
using Iaet.Catalog.Entities;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog;

public sealed class SqliteStreamCatalog : IStreamCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CatalogDbContext _db;

    public SqliteStreamCatalog(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task SaveStreamAsync(CapturedStream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _db.Streams.Add(new CapturedStreamEntity
        {
            Id = stream.Id,
            SessionId = stream.SessionId,
            Protocol = stream.Protocol.ToString(),
            Url = stream.Url,
            StartedAt = stream.StartedAt,
            EndedAt = stream.EndedAt,
            MetadataJson = JsonSerializer.Serialize(stream.Metadata.Properties, JsonOptions),
            FramesJson = stream.Frames is not null
                ? JsonSerializer.Serialize(stream.Frames, JsonOptions)
                : null,
            SamplePayloadPath = stream.SamplePayloadPath,
            Tag = stream.Tag
        });

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CapturedStream>> GetStreamsBySessionAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        var raw = await _db.Streams
            .Where(s => s.SessionId == sessionId)
            .ToListAsync(ct).ConfigureAwait(false);

        return raw.Select(MapToDomain).ToList();
    }

    public async Task<CapturedStream?> GetStreamByIdAsync(
        Guid streamId, CancellationToken ct = default)
    {
        var entity = await _db.Streams
            .FirstOrDefaultAsync(s => s.Id == streamId, ct).ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    private static CapturedStream MapToDomain(CapturedStreamEntity entity)
    {
        var properties = entity.MetadataJson is not null
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson, JsonOptions)
              ?? new Dictionary<string, string>()
            : new Dictionary<string, string>();

        IReadOnlyList<StreamFrame>? frames = null;
        if (entity.FramesJson is not null)
        {
            frames = JsonSerializer.Deserialize<List<StreamFrame>>(entity.FramesJson, JsonOptions);
        }

        return new CapturedStream
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            Protocol = Enum.Parse<StreamProtocol>(entity.Protocol),
            Url = entity.Url,
            StartedAt = entity.StartedAt,
            EndedAt = entity.EndedAt,
            Metadata = new StreamMetadata(properties),
            Frames = frames,
            SamplePayloadPath = entity.SamplePayloadPath,
            Tag = entity.Tag
        };
    }
}
