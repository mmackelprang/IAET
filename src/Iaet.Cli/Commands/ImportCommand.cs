using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Catalog;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Cli.Commands;

/// <summary>
/// Implements <c>iaet import</c>: reads a <c>.iaet.json</c> file into the catalog, or starts
/// an HTTP listener that accepts POSTed <c>.iaet.json</c> payloads from the browser extensions.
/// </summary>
internal static class ImportCommand
{
    // ---- JSON options shared across all deserialization ----

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    // ---- Command factory ----

    internal static Command Create(IServiceProvider services)
    {
        var importCmd = new Command("import", "Import a .iaet.json file into the catalog, or listen for POST uploads");

        var fileOption    = new Option<FileInfo?>("--file")    { Description = "Path to a .iaet.json file to import" };
        var projectOption = new Option<string?>("--project")  { Description = "Archive capture with this project (compressed)" };
        var listenOption = new Option<bool>("--listen")       { Description = "Start an HTTP server that accepts POST of .iaet.json" };
        var portOption   = new Option<int>("--port")          { Description = "Port for the HTTP listener (default: 7474)", DefaultValueFactory = _ => 7474 };

        importCmd.Add(fileOption);
        importCmd.Add(projectOption);
        importCmd.Add(listenOption);
        importCmd.Add(portOption);

        importCmd.SetAction(async (parseResult) =>
        {
            var file    = parseResult.GetValue(fileOption);
            var project = parseResult.GetValue(projectOption);
            var listen  = parseResult.GetValue(listenOption);
            var port    = parseResult.GetValue(portOption);

            if (file is not null && listen)
            {
                await Console.Error.WriteLineAsync("Error: --file and --listen cannot be combined. Use one or the other.").ConfigureAwait(false);
                return;
            }

            if (file is not null)
            {
                await HandleFileImport(file, services).ConfigureAwait(false);

                // Archive capture with project if specified
                if (project is not null)
                {
                    await ArchiveCaptureAsync(file, project, services).ConfigureAwait(false);
                }
                return;
            }

            if (listen)
            {
                await HandleListenMode(port, services).ConfigureAwait(false);
                return;
            }

            await Console.Error.WriteLineAsync("Error: specify --file <path> or --listen.").ConfigureAwait(false);
        });

        return importCmd;
    }

    // ---- File import ----

    private static async Task HandleFileImport(FileInfo file, IServiceProvider services)
    {
        if (!file.Exists)
        {
            await Console.Error.WriteLineAsync($"File not found: {file.FullName}").ConfigureAwait(false);
            return;
        }

        await Console.Out.WriteLineAsync($"Importing {file.FullName}...").ConfigureAwait(false);

        var stream = file.OpenRead();
        await using (stream.ConfigureAwait(false))
        {
            var iaetFile = await JsonSerializer.DeserializeAsync<IaetFileDto>(stream, JsonOptions)
                           .ConfigureAwait(false);

            if (iaetFile is null)
            {
                await Console.Error.WriteLineAsync($"Failed to parse .iaet.json — file is empty or invalid.").ConfigureAwait(false);
                return;
            }

            var (_, requestCount) = await PersistAsync(iaetFile, services).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Imported session '{iaetFile.Session?.Name}' ({requestCount} requests, 1 session record).").ConfigureAwait(false);
        }
    }

    // ---- Listen mode ----

    private static async Task HandleListenMode(int port, IServiceProvider services)
    {
        var prefix = $"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        await Console.Out.WriteLineAsync($"IAET import listener started at {prefix}").ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"POST .iaet.json payloads to this address from the browser extension.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Press Ctrl+C to stop.").ConfigureAwait(false);

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (!cts.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await listener.GetContextAsync().WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Handle in background so we can keep accepting
            _ = Task.Run(() => HandleHttpContextAsync(ctx, services), cts.Token);
        }

        listener.Stop();
        await Console.Out.WriteLineAsync($"Listener stopped.").ConfigureAwait(false);
    }

    private static async Task HandleHttpContextAsync(HttpListenerContext ctx, IServiceProvider services)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        // CORS pre-flight (extension may be cross-origin)
        res.AddHeader("Access-Control-Allow-Origin", "*");
        res.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
        res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            res.StatusCode = 204;
            res.Close();
            return;
        }

        if (!req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonResponse(res, 405, new { error = "Method Not Allowed" }).ConfigureAwait(false);
            return;
        }

        IaetFileDto? iaetFile;
        try
        {
            iaetFile = await JsonSerializer.DeserializeAsync<IaetFileDto>(req.InputStream, JsonOptions)
                       .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            await WriteJsonResponse(res, 400, new { error = $"Invalid JSON: {ex.Message}" }).ConfigureAwait(false);
            return;
        }

        if (iaetFile is null)
        {
            await WriteJsonResponse(res, 400, new { error = "Empty payload" }).ConfigureAwait(false);
            return;
        }

        try
        {
            var (_, requestCount) = await PersistAsync(iaetFile, services).ConfigureAwait(false);
            var sessionName = iaetFile.Session?.Name ?? "unknown";
            await Console.Out.WriteLineAsync($"[{DateTimeOffset.UtcNow:HH:mm:ss}] Imported '{sessionName}' — {requestCount} requests").ConfigureAwait(false);
            await WriteJsonResponse(res, 200, new { ok = true, requestCount, sessionName }).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await Console.Error.WriteLineAsync($"Import error: {ex.Message}").ConfigureAwait(false);
            await WriteJsonResponse(res, 500, new { error = ex.Message }).ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonResponse(HttpListenerResponse res, int statusCode, object body)
    {
        res.StatusCode = statusCode;
        res.ContentType = "application/json";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        res.Close();
    }

    // ---- Persistence ----

    private static async Task<(int sessionCount, int requestCount)> PersistAsync(
        IaetFileDto iaetFile, IServiceProvider services)
    {
        using var scope   = services.CreateScope();
        var db            = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var catalog       = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();
        var streamCatalog = scope.ServiceProvider.GetRequiredService<IStreamCatalog>();

        // Persist session
        var sessionDto = iaetFile.Session ?? throw new InvalidOperationException("Missing session object in .iaet.json");
        var sessionId  = Guid.TryParse(sessionDto.Id, out var sid) ? sid : Guid.NewGuid();

        // Check if session already exists; skip if so
        var existing = await catalog.ListSessionsAsync().ConfigureAwait(false);
        if (!existing.Any(s => s.Id == sessionId))
        {
            await catalog.SaveSessionAsync(new CaptureSessionInfo
            {
                Id                 = sessionId,
                Name               = sessionDto.Name ?? "imported",
                TargetApplication  = sessionDto.TargetApplication ?? "unknown",
                Profile            = sessionDto.Profile ?? "default",
                StartedAt          = DateTimeOffset.TryParse(sessionDto.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var sa) ? sa : DateTimeOffset.UtcNow,
                StoppedAt          = DateTimeOffset.TryParse(sessionDto.StoppedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var sto) ? sto : null,
            }).ConfigureAwait(false);
        }

        // Persist requests
        var requestCount = 0;
        foreach (var r in iaetFile.Requests ?? [])
        {
            var reqId = Guid.TryParse(r.Id, out var rid) ? rid : Guid.NewGuid();
            await catalog.SaveRequestAsync(new CapturedRequest
            {
                Id              = reqId,
                SessionId       = sessionId,
                Timestamp       = DateTimeOffset.TryParse(r.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts) ? ts : DateTimeOffset.UtcNow,
                HttpMethod      = r.HttpMethod ?? "GET",
                Url             = r.Url        ?? string.Empty,
                RequestHeaders  = r.RequestHeaders  ?? new Dictionary<string, string>(),
                RequestBody     = r.RequestBody,
                ResponseStatus  = r.ResponseStatus,
                ResponseHeaders = r.ResponseHeaders ?? new Dictionary<string, string>(),
                ResponseBody    = r.ResponseBody,
                DurationMs      = r.DurationMs,
                Tag             = r.Tag,
            }).ConfigureAwait(false);
            requestCount++;
        }

        // Persist streams (Phase 2 data — best-effort; skip unknown protocols)
        foreach (var s in iaetFile.Streams ?? [])
        {
            var streamId = Guid.TryParse(s.Id, out var stid) ? stid : Guid.NewGuid();
            if (!Enum.TryParse<StreamProtocol>(s.Protocol, ignoreCase: true, out var proto))
                proto = StreamProtocol.Unknown;

            DateTimeOffset.TryParse(s.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var streamStart);
            DateTimeOffset.TryParse(s.EndedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var streamEnd);

            await streamCatalog.SaveStreamAsync(new CapturedStream
            {
                Id        = streamId,
                SessionId = sessionId,
                Protocol  = proto,
                Url       = s.Url ?? string.Empty,
                StartedAt = streamStart == default ? DateTimeOffset.UtcNow : streamStart,
                EndedAt   = s.EndedAt is not null && streamEnd != default ? streamEnd : null,
                Metadata  = new StreamMetadata(s.Metadata ?? new Dictionary<string, string>()),
                Frames    = BuildFrames(s.Frames),
                SamplePayloadPath = s.SamplePayloadPath,
                Tag       = s.Tag,
            }).ConfigureAwait(false);
        }

        return (1, requestCount);
    }

    private static List<StreamFrame>? BuildFrames(List<StreamFrameDto>? frames)
    {
        if (frames is null || frames.Count == 0) return null;

        return frames
            .Select(f =>
            {
                DateTimeOffset.TryParse(f.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts);
                var direction = Enum.TryParse<StreamFrameDirection>(f.Direction, ignoreCase: true, out var d)
                    ? d : StreamFrameDirection.Received;

                byte[]? binaryPayload = null;
                if (f.BinaryPayload is not null)
                {
                    try { binaryPayload = Convert.FromBase64String(f.BinaryPayload); }
                    catch (FormatException) { /* ignore malformed base64 */ }
                }

                return new StreamFrame
                {
                    Timestamp     = ts == default ? DateTimeOffset.UtcNow : ts,
                    Direction     = direction,
                    TextPayload   = f.TextPayload,
                    BinaryPayload = binaryPayload,
                    SizeBytes     = f.SizeBytes,
                };
            })
            .ToList();
    }

    // ---- DTOs for .iaet.json deserialization ----
    // CA1812 is suppressed: these classes are instantiated by System.Text.Json via reflection.

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by System.Text.Json deserializer via reflection")]
    private sealed class IaetFileDto
    {
        [JsonPropertyName("iaetVersion")]
        public string? IaetVersion { get; init; }

        [JsonPropertyName("exportedAt")]
        public string? ExportedAt { get; init; }

        [JsonPropertyName("session")]
        public SessionDto? Session { get; init; }

        [JsonPropertyName("requests")]
        public List<RequestDto>? Requests { get; init; }

        [JsonPropertyName("streams")]
        public List<StreamDto>? Streams { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by System.Text.Json deserializer via reflection")]
    private sealed class SessionDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("targetApplication")]
        public string? TargetApplication { get; init; }

        [JsonPropertyName("profile")]
        public string? Profile { get; init; }

        [JsonPropertyName("startedAt")]
        public string? StartedAt { get; init; }

        [JsonPropertyName("stoppedAt")]
        public string? StoppedAt { get; init; }

        [JsonPropertyName("capturedBy")]
        public string? CapturedBy { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by System.Text.Json deserializer via reflection")]
    private sealed class RequestDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("sessionId")]
        public string? SessionId { get; init; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; init; }

        [JsonPropertyName("httpMethod")]
        public string? HttpMethod { get; init; }

        [JsonPropertyName("url")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "DTO mirrors JSON field")]
        public string? Url { get; init; }

        [JsonPropertyName("requestHeaders")]
        public Dictionary<string, string>? RequestHeaders { get; init; }

        [JsonPropertyName("requestBody")]
        public string? RequestBody { get; init; }

        [JsonPropertyName("responseStatus")]
        public int ResponseStatus { get; init; }

        [JsonPropertyName("responseHeaders")]
        public Dictionary<string, string>? ResponseHeaders { get; init; }

        [JsonPropertyName("responseBody")]
        public string? ResponseBody { get; init; }

        [JsonPropertyName("durationMs")]
        public long DurationMs { get; init; }

        [JsonPropertyName("tag")]
        public string? Tag { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by System.Text.Json deserializer via reflection")]
    private sealed class StreamDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("sessionId")]
        public string? SessionId { get; init; }

        [JsonPropertyName("protocol")]
        public string? Protocol { get; init; }

        [JsonPropertyName("url")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "DTO mirrors JSON field")]
        public string? Url { get; init; }

        [JsonPropertyName("startedAt")]
        public string? StartedAt { get; init; }

        [JsonPropertyName("endedAt")]
        public string? EndedAt { get; init; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string>? Metadata { get; init; }

        [JsonPropertyName("frames")]
        public List<StreamFrameDto>? Frames { get; init; }

        [JsonPropertyName("samplePayloadPath")]
        public string? SamplePayloadPath { get; init; }

        [JsonPropertyName("tag")]
        public string? Tag { get; init; }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by System.Text.Json deserializer via reflection")]
    private sealed class StreamFrameDto
    {
        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; init; }

        [JsonPropertyName("direction")]
        public string? Direction { get; init; }

        [JsonPropertyName("textPayload")]
        public string? TextPayload { get; init; }

        [JsonPropertyName("binaryPayload")]
        public string? BinaryPayload { get; init; }

        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; init; }
    }

    // ---- Capture archival ----

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private static async Task ArchiveCaptureAsync(FileInfo captureFile, string projectName, IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();
            var projectStore = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var projectDir = projectStore.GetProjectDirectory(projectName);

            var capturesDir = Path.Combine(projectDir, "captures");
            Directory.CreateDirectory(capturesDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var archiveName = $"{timestamp}-{Path.GetFileNameWithoutExtension(captureFile.Name)}.iaet.json.gz";
            var archivePath = Path.Combine(capturesDir, archiveName);

            var inputStream = captureFile.OpenRead();
            await using (inputStream.ConfigureAwait(false))
            {
            var outputStream = File.Create(archivePath);
            await using (outputStream.ConfigureAwait(false))
            {
            var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal);
            await using (gzipStream.ConfigureAwait(false))
            {
            await inputStream.CopyToAsync(gzipStream).ConfigureAwait(false);
            } // gzipStream
            } // outputStream
            } // inputStream

            var originalSize = captureFile.Length;
            var compressedSize = new FileInfo(archivePath).Length;
            var ratio = originalSize > 0 ? (compressedSize * 100) / originalSize : 0;

            Console.WriteLine($"Archived capture to project '{projectName}': {archiveName} ({compressedSize / 1024}KB, {ratio}% of original)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not archive capture to project: {ex.Message}");
        }
    }
}
