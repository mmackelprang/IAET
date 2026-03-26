using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

/// <summary>
/// Generates a HAR 1.2 JSON document from an <see cref="ExportContext"/>.
/// </summary>
public static class HarGenerator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Generates the HAR 1.2 JSON string.</summary>
    public static string Generate(ExportContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var har = new HarRoot(
            Log: new HarLog(
                Version: "1.2",
                Creator: new HarCreator(Name: "IAET", Version: "0.1.0"),
                Entries: [.. ctx.Requests.Select(BuildEntry)]));

        return JsonSerializer.Serialize(har, SerializerOptions);
    }

    // ------------------------------------------------------------------

    private static HarEntry BuildEntry(CapturedRequest req)
    {
        return new HarEntry(
            StartedDateTime: req.Timestamp.ToString("o"),
            Time: req.DurationMs,
            Request: BuildRequest(req),
            Response: BuildResponse(req),
            Timings: new HarTimings(Wait: req.DurationMs));
    }

    private static HarRequest BuildRequest(CapturedRequest req)
    {
        var uri      = new Uri(req.Url);
        var headers  = req.RequestHeaders
                          .Select(kv => new HarNameValue(Name: kv.Key, Value: HeaderRedactor.RedactHeaderValue(kv.Key, kv.Value)))
                          .ToArray();
        var queryParams = uri.Query.TrimStart('?')
                             .Split('&', StringSplitOptions.RemoveEmptyEntries)
                             .Select(p =>
                             {
                                 var idx = p.IndexOf('=', StringComparison.Ordinal);
                                 return idx >= 0
                                     ? new HarNameValue(Name: p[..idx], Value: p[(idx + 1)..])
                                     : new HarNameValue(Name: p, Value: string.Empty);
                             })
                             .ToArray();

        HarPostData? postData = null;
        if (!string.IsNullOrEmpty(req.RequestBody))
        {
            postData = new HarPostData(
                MimeType: req.RequestHeaders.TryGetValue("Content-Type", out var ct) ? ct : "application/octet-stream",
                Text: req.RequestBody);
        }

        return new HarRequest(
            Method: req.HttpMethod,
            Url: req.Url,
            HttpVersion: "HTTP/1.1",
            Headers: headers,
            QueryString: queryParams,
            PostData: postData,
            HeadersSize: -1,
            BodySize: req.RequestBody is not null ? Encoding.UTF8.GetByteCount(req.RequestBody) : 0);
    }

    private static HarResponse BuildResponse(CapturedRequest req)
    {
        var headers = req.ResponseHeaders
                        .Select(kv => new HarNameValue(Name: kv.Key, Value: HeaderRedactor.RedactHeaderValue(kv.Key, kv.Value)))
                        .ToArray();

        return new HarResponse(
            Status: req.ResponseStatus,
            StatusText: GetStatusText(req.ResponseStatus),
            HttpVersion: "HTTP/1.1",
            Headers: headers,
            Content: new HarContent(
                Size: req.ResponseBody is not null ? Encoding.UTF8.GetByteCount(req.ResponseBody) : 0,
                MimeType: req.ResponseHeaders.TryGetValue("Content-Type", out var ct) ? ct : "application/octet-stream",
                Text: req.ResponseBody),
            RedirectUrl: string.Empty,
            HeadersSize: -1,
            BodySize: req.ResponseBody is not null ? Encoding.UTF8.GetByteCount(req.ResponseBody) : 0);
    }

    private static string GetStatusText(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        500 => "Internal Server Error",
        _   => string.Empty,
    };

    // ------------------------------------------------------------------ DTOs

    private sealed record HarRoot(
        [property: JsonPropertyName("log")] HarLog Log);

    private sealed record HarLog(
        [property: JsonPropertyName("version")] string     Version,
        [property: JsonPropertyName("creator")] HarCreator Creator,
        [property: JsonPropertyName("entries")] HarEntry[] Entries);

    private sealed record HarCreator(
        [property: JsonPropertyName("name")]    string Name,
        [property: JsonPropertyName("version")] string Version);

    private sealed record HarEntry(
        [property: JsonPropertyName("startedDateTime")] string      StartedDateTime,
        [property: JsonPropertyName("time")]            long        Time,
        [property: JsonPropertyName("request")]         HarRequest  Request,
        [property: JsonPropertyName("response")]        HarResponse Response,
        [property: JsonPropertyName("timings")]         HarTimings  Timings);

    private sealed record HarRequest(
        [property: JsonPropertyName("method")]      string         Method,
        [property: JsonPropertyName("url")]         string         Url,
        [property: JsonPropertyName("httpVersion")] string         HttpVersion,
        [property: JsonPropertyName("headers")]     HarNameValue[] Headers,
        [property: JsonPropertyName("queryString")] HarNameValue[] QueryString,
        [property: JsonPropertyName("postData")]    HarPostData?   PostData,
        [property: JsonPropertyName("headersSize")] int            HeadersSize,
        [property: JsonPropertyName("bodySize")]    int            BodySize);

    private sealed record HarResponse(
        [property: JsonPropertyName("status")]      int            Status,
        [property: JsonPropertyName("statusText")]  string         StatusText,
        [property: JsonPropertyName("httpVersion")] string         HttpVersion,
        [property: JsonPropertyName("headers")]     HarNameValue[] Headers,
        [property: JsonPropertyName("content")]     HarContent     Content,
        [property: JsonPropertyName("redirectURL")] string         RedirectUrl,
        [property: JsonPropertyName("headersSize")] int            HeadersSize,
        [property: JsonPropertyName("bodySize")]    int            BodySize);

    private sealed record HarNameValue(
        [property: JsonPropertyName("name")]  string Name,
        [property: JsonPropertyName("value")] string Value);

    private sealed record HarPostData(
        [property: JsonPropertyName("mimeType")] string MimeType,
        [property: JsonPropertyName("text")]     string Text);

    private sealed record HarContent(
        [property: JsonPropertyName("size")]     int     Size,
        [property: JsonPropertyName("mimeType")] string  MimeType,
        [property: JsonPropertyName("text")]     string? Text);

    private sealed record HarTimings(
        [property: JsonPropertyName("wait")] long Wait);
}
