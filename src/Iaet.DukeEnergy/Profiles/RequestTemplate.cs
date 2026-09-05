using System.Diagnostics.CodeAnalysis;

namespace Iaet.DukeEnergy.Profiles;

/// <summary>
/// A single captured HTTP request, parameterized so it can be replayed with different values.
/// </summary>
/// <param name="Method">HTTP method, for example <c>POST</c>.</param>
/// <param name="UrlTemplate">
/// Absolute URL, or a path resolved against <see cref="OutageReportProfile.BaseUri"/>. May contain
/// <c>{{token}}</c> placeholders, which are URL-encoded on substitution.
/// </param>
/// <param name="Headers">Request headers; values may contain <c>{{token}}</c> placeholders.</param>
/// <param name="Body">Request body; values substituted into it are JSON-escaped.</param>
/// <param name="ContentType">Body media type. Defaults to <c>application/json</c>.</param>
/// <param name="ResponseMap">
/// Maps well-known field names onto dotted paths into the response body, for example
/// <c>{ "accountNumber": "data.accounts[0].accountNumber" }</c>.
/// </param>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
    Justification = "Carries {{token}} placeholders, so it is not a well-formed URI until rendered.")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
    Justification = "Carries {{token}} placeholders, so it is not a well-formed URI until rendered.")]
public sealed record RequestTemplate(
    string Method,
    string UrlTemplate,
    IReadOnlyDictionary<string, string>? Headers = null,
    string? Body = null,
    string? ContentType = null,
    IReadOnlyDictionary<string, string>? ResponseMap = null);
