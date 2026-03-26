namespace Iaet.Catalog.Entities;

public class CapturedStreamEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public required string Protocol { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? MetadataJson { get; set; }
    public string? FramesJson { get; set; }
    public string? SamplePayloadPath { get; set; }
    public string? Tag { get; set; }
    public CaptureSessionEntity? Session { get; set; }
}
