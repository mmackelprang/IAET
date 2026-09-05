# Iaet.DukeEnergy

A client for Duke Energy's outage systems: the public outage map, and the account-scoped
outage-report flow behind <https://outagereport.duke-energy.com>.

Neither API is documented by Duke Energy. The map half is reconstructed from public sources and
works out of the box; the report half is driven by an endpoint profile captured with IAET, so no
URL or payload in it is guessed.

See [docs/duke-energy-rest-interface.md](../../docs/duke-energy-rest-interface.md) for the full
guide, and `Iaet.DukeEnergy.Api` for the REST service built on this library.

---

## Registration

```csharp
services.AddDukeEnergy(options =>
{
    options.Jurisdiction    = Jurisdictions.Carolinas;
    options.Home.Latitude   = 35.7796;
    options.Home.Longitude  = -78.6382;
    options.Home.RadiusMiles = 1.5;
});
```

Registers `IOutageMapClient`, `IOutageReportClient` and `IHomeOutageService`, each on a resilient
`HttpClient`.

---

## Key types

### `IOutageMapClient`

```csharp
Task<IReadOnlyList<CountyOutageSummary>> GetCountiesAsync(string? jurisdiction = null, CancellationToken ct = default);
Task<IReadOnlyList<OutageEvent>>         GetOutagesAsync(string? jurisdiction = null, CancellationToken ct = default);
Task<NeighborhoodOutageReport>           GetNeighborhoodAsync(double lat, double lon, double radiusMiles, string? jurisdiction = null, CancellationToken ct = default);
```

Reads the public map. Credentials come from the map's own configuration document via
`IDukeEnergyCredentialProvider`, are cached for an hour, and are re-fetched once automatically if
the API starts rejecting them. Responses are cached for `OutageCacheDuration` (default two
minutes) because Duke refreshes roughly every fifteen.

`GetNeighborhoodAsync` filters by great-circle distance (`GeoMath.DistanceMiles`) and returns
results nearest first, each tagged with `DistanceMiles`.

### `IOutageReportClient`

```csharp
bool IsConfigured { get; }
string? ConfigurationProblem { get; }

Task<AccountLookupResult>  LookupAccountByPhoneAsync(string phoneNumber, CancellationToken ct = default);
Task<AccountLookupResult>  LookupAccountByNumberAsync(string accountNumber, CancellationToken ct = default);
Task<AccountOutageStatus>  GetExistingOutageAsync(string accountNumber, CancellationToken ct = default);
Task<OutageReportReceipt>  SubmitReportAsync(OutageReportRequest request, CancellationToken ct = default);
```

Driven by `OutageReportProfile`. When no usable profile is loaded, `IsConfigured` is `false` and
`ConfigurationProblem` says exactly what is missing, so callers can surface that instead of failing
opaquely.

Submission is gated: `Report.Enabled` and `Report.AllowSubmit` must both be set, the request must
name the configured account, and `Report.MaxSubmissionsPerDay` caps a rolling 24 hours. Filing an
outage report dispatches real work at a utility, so each gate throws `InvalidOperationException`
naming itself rather than silently proceeding.

### `IAddressGeocoder` and `IAddressOutageService`

```csharp
Task<GeocodedAddress?>    GeocodeAsync(string address, CancellationToken ct = default);
Task<AddressOutageReport?> GetByAddressAsync(string address, double? radiusMiles = null, string? jurisdiction = null, CancellationToken ct = default);
```

`CensusAddressGeocoder` resolves US addresses through the US Census Bureau geocoder — free, no API
key, and matching Duke's US footprint. Results are cached for a day because addresses do not move.

`AddressOutageService` geocodes then runs the neighbourhood search, defaulting to a tighter 0.25
mile radius since an address query asks about one premises. The result carries a `Caveat` string
stating in plain language that this is proximity, not a per-meter status: Duke plots outages at
device and transformer locations rather than premises, so a nearby event does not prove this
address is affected and finding none does not prove it has power. The caveat travels in the payload
so a consumer cannot mistake the answer for an authoritative one.

For an authoritative per-premises answer, resolve the account — `LookupAccountByPhoneAsync` or
`LookupAccountByNumberAsync` both return the `ServiceAddress` Duke holds on the account.

### `IHomeOutageService`

```csharp
Task<HomeOutageStatus> GetHomeStatusAsync(CancellationToken ct = default);
```

Combines the account outage status, nearby outages and the county rollup, and resolves the
account's authoritative `ServiceAddress` along the way. Each source is optional
and its failures are isolated: a missing profile or an unreachable map degrades one section to
`null` and appends the reason to `Notes`, so a partial answer is still returned.

### `OutageJsonParser`

Normalizes map payloads into `CountyOutageSummary` and `OutageEvent`. Matches field names
case-insensitively against several known spellings, unwraps `data` / `results` / GeoJSON
`features` envelopes, reads GeoJSON `[lon, lat]` coordinate order, and accepts ISO-8601 or Unix
epoch timestamps. A field it cannot find becomes `null` rather than failing the response — Duke
has renamed these before.

### `OutageReportProfile` and `TemplateRequestExecutor`

A captured request with `{{token}}` placeholders, plus a `responseMap` of dotted paths
(`data.accounts[0].accountNumber`) onto well-known field names. Substituted values are
percent-encoded in URLs and JSON-escaped in bodies; `{{env:NAME}}` reads from the environment so
credentials stay out of the profile document.
