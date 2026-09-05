using System.Text.Json;
using System.Text.Json.Serialization;

namespace Iaet.DukeEnergy.Profiles;

/// <summary>
/// The captured shape of Duke Energy's outage-report flow.
/// </summary>
/// <remarks>
/// Duke Energy does not publish this API, so its URLs, payloads and field names are discovered by
/// capturing the outage-report web app with IAET rather than hard-coded here. Keeping the flow in
/// data means a change on Duke's side is a profile edit, not a code change.
/// </remarks>
/// <param name="Description">Free-text note about where and when the profile was captured.</param>
/// <param name="BaseUri">Base address that relative <see cref="RequestTemplate.UrlTemplate"/> values resolve against.</param>
/// <param name="DefaultHeaders">Headers applied to every request in the profile.</param>
/// <param name="LookupAccount">Resolves an account from a phone number.</param>
/// <param name="ExistingOutage">Reads the outage currently on file for an account.</param>
/// <param name="SubmitReport">Files a new outage report.</param>
public sealed record OutageReportProfile(
    string? Description = null,
    Uri? BaseUri = null,
    IReadOnlyDictionary<string, string>? DefaultHeaders = null,
    RequestTemplate? LookupAccount = null,
    RequestTemplate? ExistingOutage = null,
    RequestTemplate? SubmitReport = null)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Loads a profile from a JSON file.</summary>
    /// <param name="path">Path to the profile document.</param>
    /// <returns>The parsed profile.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidOperationException">The file is not a valid profile document.</exception>
    public static OutageReportProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Outage-report endpoint profile not found: {path}", path);
        }

        var json = File.ReadAllText(path);

        OutageReportProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<OutageReportProfile>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Outage-report endpoint profile is not valid JSON: {path}", ex);
        }

        return profile ?? throw new InvalidOperationException($"Outage-report endpoint profile is empty: {path}");
    }

    /// <summary>
    /// Whether the template still carries the placeholder values shipped in the sample profile,
    /// meaning no capture has been merged into it yet.
    /// </summary>
    public bool IsPlaceholder =>
        LookupAccount is null
        || LookupAccount.UrlTemplate.Contains("REPLACE_ME", StringComparison.OrdinalIgnoreCase);
}
