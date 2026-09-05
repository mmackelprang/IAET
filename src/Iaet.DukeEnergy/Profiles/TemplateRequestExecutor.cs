using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Iaet.DukeEnergy.Profiles;

/// <summary>
/// Renders a <see cref="RequestTemplate"/> against a set of variables, sends it, and extracts the
/// fields named by the template's response map.
/// </summary>
public sealed class TemplateRequestExecutor
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="TemplateRequestExecutor"/> class.</summary>
    /// <param name="httpClient">Client used to send rendered requests.</param>
    public TemplateRequestExecutor(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>Renders and sends a template, then extracts its mapped response fields.</summary>
    /// <param name="template">The template to execute.</param>
    /// <param name="profile">The profile the template belongs to, for base URI and shared headers.</param>
    /// <param name="variables">Values substituted into <c>{{token}}</c> placeholders.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response status, body, and extracted fields.</returns>
    public async Task<TemplateResponse> ExecuteAsync(
        RequestTemplate template,
        OutageReportProfile profile,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(variables);

        using var request = Render(template, profile, variables);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var fields = ExtractFields(template, body);

        return new TemplateResponse((int)response.StatusCode, response.IsSuccessStatusCode, body, fields);
    }

    /// <summary>Renders a template into an <see cref="HttpRequestMessage"/> without sending it.</summary>
    /// <param name="template">The template to render.</param>
    /// <param name="profile">The profile the template belongs to.</param>
    /// <param name="variables">Values substituted into <c>{{token}}</c> placeholders.</param>
    /// <returns>The rendered request. The caller owns its lifetime.</returns>
    public static HttpRequestMessage Render(
        RequestTemplate template,
        OutageReportProfile profile,
        IReadOnlyDictionary<string, string?> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(variables);

        var url = Substitute(template.UrlTemplate, variables, TokenEncoding.Url);

        // Uri.TryCreate with UriKind.Absolute accepts a leading-slash path as a file: URI on Unix,
        // so an absolute match only counts when it actually carries an HTTP scheme.
        var isAbsolute = Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));

        var uri = isAbsolute
            ? absolute!
            : profile.BaseUri is not null
                ? new Uri(profile.BaseUri, url)
                : throw new InvalidOperationException(
                    $"Template URL '{template.UrlTemplate}' is relative but the profile has no baseUri.");

        var request = new HttpRequestMessage(new HttpMethod(template.Method), uri);

        if (template.Body is { Length: > 0 })
        {
            request.Content = new StringContent(
                Substitute(template.Body, variables, TokenEncoding.Json),
                Encoding.UTF8,
                template.ContentType ?? "application/json");
        }

        foreach (var (name, value) in EnumerateHeaders(template, profile))
        {
            var rendered = Substitute(value, variables, TokenEncoding.None);

            if (!request.Headers.TryAddWithoutValidation(name, rendered))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, rendered);
            }
        }

        return request;
    }

    private static IEnumerable<KeyValuePair<string, string>> EnumerateHeaders(
        RequestTemplate template,
        OutageReportProfile profile)
    {
        if (profile.DefaultHeaders is not null)
        {
            foreach (var header in profile.DefaultHeaders)
            {
                // A template header of the same name wins over the profile default.
                if (template.Headers?.ContainsKey(header.Key) != true)
                {
                    yield return header;
                }
            }
        }

        if (template.Headers is not null)
        {
            foreach (var header in template.Headers)
            {
                yield return header;
            }
        }
    }

    private static Dictionary<string, string?> ExtractFields(RequestTemplate template, string body)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (template.ResponseMap is null || template.ResponseMap.Count == 0 || string.IsNullOrWhiteSpace(body))
        {
            return fields;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return fields;
        }

        using (document)
        {
            foreach (var (name, path) in template.ResponseMap)
            {
                fields[name] = JsonPathLite.SelectString(document.RootElement, path);
            }
        }

        return fields;
    }

    /// <summary>
    /// Replaces <c>{{token}}</c> placeholders. <c>{{env:NAME}}</c> resolves from the environment so
    /// that bearer tokens and other secrets stay out of the profile document.
    /// </summary>
    internal static string Substitute(
        string template,
        IReadOnlyDictionary<string, string?> variables,
        TokenEncoding encoding)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return template;
        }

        var result = new StringBuilder(template.Length);
        var index  = 0;

        while (index < template.Length)
        {
            var open = template.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0)
            {
                result.Append(template, index, template.Length - index);
                break;
            }

            var close = template.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                result.Append(template, index, template.Length - index);
                break;
            }

            result.Append(template, index, open - index);

            var token = template[(open + 2)..close].Trim();
            result.Append(Encode(Resolve(token, variables), encoding));

            index = close + 2;
        }

        return result.ToString();
    }

    private static string Resolve(string token, IReadOnlyDictionary<string, string?> variables)
    {
        if (token.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.GetEnvironmentVariable(token[4..]) ?? string.Empty;
        }

        return variables.TryGetValue(token, out var value) ? value ?? string.Empty : string.Empty;
    }

    private static string Encode(string value, TokenEncoding encoding) => encoding switch
    {
        TokenEncoding.Url  => Uri.EscapeDataString(value),
        TokenEncoding.Json => JsonEscape(value),
        _                  => value,
    };

    private static string JsonEscape(string value)
    {
        // Serializing to a JSON string and trimming the quotes gives correct escaping for values
        // interpolated into a JSON body template.
        var encoded = JsonSerializer.Serialize(value, JsonContext.StringOptions);
        return encoded.Length >= 2 ? encoded[1..^1] : encoded;
    }

    /// <summary>How a substituted value is escaped for the position it is being placed into.</summary>
    internal enum TokenEncoding
    {
        /// <summary>Inserted verbatim.</summary>
        None = 0,

        /// <summary>Percent-encoded for use in a URL.</summary>
        Url = 1,

        /// <summary>Escaped for use inside a JSON string literal.</summary>
        Json = 2,
    }

    private static class JsonContext
    {
        internal static readonly JsonSerializerOptions StringOptions = new(JsonSerializerDefaults.General);
    }
}

/// <summary>The outcome of executing a <see cref="RequestTemplate"/>.</summary>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="IsSuccess">Whether the status code indicates success.</param>
/// <param name="Body">The raw response body.</param>
/// <param name="Fields">Fields extracted by the template's response map.</param>
public sealed record TemplateResponse(
    int StatusCode,
    bool IsSuccess,
    string Body,
    IReadOnlyDictionary<string, string?> Fields)
{
    /// <summary>Reads a mapped field, or <see langword="null"/> when it was not extracted.</summary>
    /// <param name="name">The field name from the response map.</param>
    /// <returns>The extracted value.</returns>
    public string? Field(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Fields.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Reads a mapped field as a boolean, treating common truthy spellings as true.</summary>
    /// <param name="name">The field name from the response map.</param>
    /// <returns>The parsed value, or <see langword="null"/> when absent or unparseable.</returns>
    public bool? Flag(string name)
    {
        var value = Field(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number != 0;
        }

        return value.Trim() switch
        {
            "Y" or "y" or "yes" or "YES" or "Yes" => true,
            "N" or "n" or "no" or "NO" or "No"    => false,
            _                                     => null,
        };
    }

    /// <summary>Reads a mapped field as a timestamp.</summary>
    /// <param name="name">The field name from the response map.</param>
    /// <returns>The parsed timestamp, or <see langword="null"/> when absent or unparseable.</returns>
    public DateTimeOffset? Timestamp(string name)
    {
        var value = Field(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
        {
            return Math.Abs(epoch) > 100_000_000_000L
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
