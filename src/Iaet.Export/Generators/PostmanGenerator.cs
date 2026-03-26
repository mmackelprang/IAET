using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Iaet.Core.Models;

namespace Iaet.Export.Generators;

/// <summary>
/// Generates a Postman Collection v2.1.0 JSON document from an <see cref="ExportContext"/>.
/// </summary>
public static class PostmanGenerator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Preserve < > characters as-is (e.g. <REDACTED> placeholders must be human-readable)
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Generates the Postman Collection JSON string.</summary>
    public static string Generate(ExportContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var collection = new PostmanCollection(
            Info: new PostmanInfo(
                Name: ctx.Session.TargetApplication + " API",
                Schema: "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"),
            Item: BuildItems(ctx));

        return JsonSerializer.Serialize(collection, SerializerOptions);
    }

    // ------------------------------------------------------------------

    private static PostmanItem[] BuildItems(ExportContext ctx)
    {
        var items = new List<PostmanItem>();

        foreach (var group in ctx.EndpointGroups)
        {
            var firstRequest = FindFirstRequest(ctx, group);

            var headers = BuildHeaders(firstRequest);
            var body    = BuildBody(firstRequest);

            items.Add(new PostmanItem(
                Name: $"{group.Signature.Method} {group.Signature.NormalizedPath}",
                Request: new PostmanRequest(
                    Method: group.Signature.Method,
                    Header: headers,
                    Body: body,
                    Url: new PostmanUrl(
                        Raw: firstRequest?.Url ?? string.Empty))));
        }

        return [.. items];
    }

    private static CapturedRequest? FindFirstRequest(ExportContext ctx, EndpointGroup group)
    {
        foreach (var req in ctx.Requests)
        {
            var sig = EndpointSignature.FromRequest(req.HttpMethod, new Uri(req.Url).AbsolutePath);
            if (sig.Normalized == group.Signature.Normalized)
                return req;
        }

        return null;
    }

    private static PostmanHeader[] BuildHeaders(CapturedRequest? req)
    {
        if (req is null || req.RequestHeaders.Count == 0)
            return [];

        return [.. req.RequestHeaders.Select(kv =>
            new PostmanHeader(Key: kv.Key, Value: HeaderRedactor.RedactHeaderValue(kv.Key, kv.Value)))];
    }

    private static PostmanBody? BuildBody(CapturedRequest? req)
    {
        if (req is null || string.IsNullOrEmpty(req.RequestBody))
            return null;

        return new PostmanBody(Mode: "raw", Raw: req.RequestBody);
    }

    // ------------------------------------------------------------------  DTOs

    private sealed record PostmanCollection(
        [property: JsonPropertyName("info")]  PostmanInfo   Info,
        [property: JsonPropertyName("item")]  PostmanItem[] Item);

    private sealed record PostmanInfo(
        [property: JsonPropertyName("name")]   string Name,
        [property: JsonPropertyName("schema")] string Schema);

    private sealed record PostmanItem(
        [property: JsonPropertyName("name")]    string       Name,
        [property: JsonPropertyName("request")] PostmanRequest Request);

    private sealed record PostmanRequest(
        [property: JsonPropertyName("method")] string         Method,
        [property: JsonPropertyName("header")] PostmanHeader[] Header,
        [property: JsonPropertyName("body")]   PostmanBody?   Body,
        [property: JsonPropertyName("url")]    PostmanUrl     Url);

    private sealed record PostmanHeader(
        [property: JsonPropertyName("key")]   string Key,
        [property: JsonPropertyName("value")] string Value);

    private sealed record PostmanBody(
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("raw")]  string Raw);

    private sealed record PostmanUrl(
        [property: JsonPropertyName("raw")] string Raw);
}
