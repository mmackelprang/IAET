namespace Iaet.DukeEnergy;

/// <summary>
/// Configuration for the Duke Energy outage client.
/// </summary>
/// <remarks>
/// Defaults target the public outage-map API used by <c>outagemap.duke-energy.com</c>. The
/// account-scoped outage-report flow has no defaults: it is driven entirely by an endpoint
/// profile captured with IAET (see <see cref="OutageReportOptions.ProfilePath"/>).
/// </remarks>
public sealed class DukeEnergyOptions
{
    /// <summary>
    /// Location of the outage-map front-end configuration document. The API consumer key and
    /// secret used for Basic authentication are read from this document.
    /// </summary>
    public Uri ConfigUri { get; set; } = new("https://outagemap.duke-energy.com/config/config.prod.json");

    /// <summary>Base address of the outage-map API.</summary>
    public Uri OutageMapBaseUri { get; set; } = new("https://cust-api.duke-energy.com");

    /// <summary>Path of the per-county outage summary resource.</summary>
    public string CountiesPath { get; set; } = "/outage-maps/v1/counties";

    /// <summary>Path of the individual outage event resource.</summary>
    public string OutagesPath { get; set; } = "/outage-maps/v1/outages";

    /// <summary>
    /// Default operating-company code sent as the <c>jurisdiction</c> query parameter.
    /// See <see cref="Jurisdictions"/>.
    /// </summary>
    public string Jurisdiction { get; set; } = Jurisdictions.Carolinas;

    /// <summary>Value sent as the <c>Origin</c> and <c>Referer</c> headers.</summary>
    public Uri Origin { get; set; } = new("https://outagemap.duke-energy.com");

    /// <summary>Value sent as the <c>User-Agent</c> header.</summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

    /// <summary>How long the API consumer key and secret are cached before being re-fetched.</summary>
    public TimeSpan CredentialCacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long outage responses are cached. Duke publishes updates roughly every 15 minutes, so
    /// polling faster than this only adds load without adding information.
    /// </summary>
    public TimeSpan OutageCacheDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Per-request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The service location this client reports on by default.</summary>
    public HomeOptions Home { get; } = new();

    /// <summary>Settings for the account-scoped outage-report flow.</summary>
    public OutageReportOptions Report { get; } = new();

    /// <summary>Settings for address geocoding.</summary>
    public GeocoderOptions Geocoder { get; } = new();
}

/// <summary>
/// Settings for resolving street addresses to coordinates.
/// </summary>
public sealed class GeocoderOptions
{
    /// <summary>Base address of the geocoding service.</summary>
    public Uri BaseUri { get; set; } =
        new("https://geocoding.geo.census.gov/geocoder/locations/onelineaddress");

    /// <summary>Which Census benchmark to resolve against.</summary>
    public string Benchmark { get; set; } = "Public_AR_Current";

    /// <summary>
    /// Default radius for an address query, in miles. Tighter than the neighborhood default: an
    /// address query is asking about one premises, so a wide radius mostly adds false positives.
    /// </summary>
    public double DefaultRadiusMiles { get; set; } = 0.25;

    /// <summary>How long geocoding results are cached. Addresses do not move.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>
/// Identifies the service location treated as "home" by <see cref="Abstractions.IHomeOutageService"/>.
/// </summary>
public sealed class HomeOptions
{
    /// <summary>Friendly label echoed back in responses, for example <c>"123 Main St"</c>.</summary>
    public string? Label { get; set; }

    /// <summary>Latitude of the service location, in degrees.</summary>
    public double? Latitude { get; set; }

    /// <summary>Longitude of the service location, in degrees.</summary>
    public double? Longitude { get; set; }

    /// <summary>Radius treated as "the neighborhood", in miles.</summary>
    public double RadiusMiles { get; set; } = 1.0;

    /// <summary>Operating-company code for this location; falls back to <see cref="DukeEnergyOptions.Jurisdiction"/>.</summary>
    public string? Jurisdiction { get; set; }

    /// <summary>County name, used for the county-level rollup.</summary>
    public string? County { get; set; }

    /// <summary>Two-letter state code, used for the county-level rollup.</summary>
    public string? State { get; set; }

    /// <summary>Phone number on the Duke Energy account, used for account lookup.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Duke Energy account number, if already known.</summary>
    public string? AccountNumber { get; set; }
}

/// <summary>
/// Settings for the account-scoped outage-report flow (account lookup, existing-outage status,
/// and outage submission).
/// </summary>
/// <remarks>
/// Both gates default to <see langword="false"/>. Reading and writing account-scoped data requires
/// an endpoint profile plus an explicit opt-in, so a misconfigured deployment cannot file an outage
/// report against a real utility by accident.
/// </remarks>
public sealed class OutageReportOptions
{
    /// <summary>Enables account lookup and existing-outage status queries.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Enables submission of new outage reports. Requires <see cref="Enabled"/> as well.
    /// </summary>
    public bool AllowSubmit { get; set; }

    /// <summary>
    /// Path to the JSON endpoint profile describing the outage-report requests. See
    /// <c>profiles/duke-outage-report.template.json</c> for the schema and
    /// <c>docs/duke-energy-rest-interface.md</c> for how to fill it in from an IAET capture.
    /// </summary>
    public string? ProfilePath { get; set; }

    /// <summary>Maximum outage reports this client will submit in any rolling 24-hour window.</summary>
    public int MaxSubmissionsPerDay { get; set; } = 5;

    /// <summary>
    /// When <see langword="true"/>, submissions are validated and logged but never sent.
    /// </summary>
    public bool DryRun { get; set; }
}
