# Duke Energy outage REST interface

A local REST service for finding and reporting outages at your own home and in your
neighbourhood, backed by Duke Energy's outage systems.

Target: <https://outagereport.duke-energy.com/#/report-outage/home/find-account/phone-number/usecase/existing-outage>

---

## What works today, and what needs a capture

The service has two halves, and they are at different levels of confidence. This distinction is
the single most important thing on this page.

| Half | Endpoints | Status |
|------|-----------|--------|
| **Public outage map** — neighbourhood and county outages | `/api/v1/outages/*` | **Works now.** The request shape is verified against public consumers of the same API. |
| **Account-scoped flow** — account lookup, existing-outage status, filing a report | `/api/v1/accounts/*`, `/api/v1/outages/report` | **Needs a capture.** Wired end to end, but driven by an endpoint profile you fill in. Returns `503` with an explanation until you do. |

Duke Energy publishes no documentation for either API. The map half could be reconstructed from
public sources; the account half could not, and the endpoints for it were deliberately **not
guessed** — a guessed URL that returns `404`, or worse a guessed body that files the wrong thing,
is more expensive than an honest `503`. Section [Filling in the account flow](#filling-in-the-account-flow)
is the twenty-minute capture that closes the gap.

---

## Quick start

```bash
# Nearby outages, no configuration and no credentials needed
iaet duke neighborhood --lat 35.7796 --lon -78.6382 --radius 1.5 --jurisdiction DEC

# Or run the REST service
iaet duke serve --port 9300 --settings dukeenergy.settings.json
```

```bash
curl "http://localhost:9300/api/v1/outages/neighborhood?lat=35.7796&lon=-78.6382&radiusMiles=1.5"
curl  http://localhost:9300/api/v1/home/status
```

The service binds to loopback only unless you pass `--all-interfaces`. It holds your account
number and can file outage reports, so it should not be exposed to a network you do not control.

---

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET`  | `/health` | Liveness check. |
| `GET`  | `/api/v1/jurisdictions` | Operating-company codes this service understands. |
| `GET`  | `/api/v1/outages?jurisdiction=DEC` | Every outage the map reports for a jurisdiction. |
| `GET`  | `/api/v1/outages/counties?jurisdiction=DEC` | Per-county rollup: customers served, customers affected, percent out. |
| `GET`  | `/api/v1/outages/neighborhood?lat=&lon=&radiusMiles=&jurisdiction=` | Outages within a radius of a point, nearest first. All parameters fall back to the configured home. |
| `GET`  | `/api/v1/home/status` | The combined answer: account outage status + nearby outages + county rollup. |
| `POST` | `/api/v1/accounts/lookup` | Resolve an account from a phone number. Body: `{"phoneNumber":"9195550100"}`. |
| `GET`  | `/api/v1/accounts/{accountNumber}/outage` | The outage Duke Energy currently has on file for an account. |
| `POST` | `/api/v1/outages/report` | File a new outage report. Body: `{"accountNumber":"...","phoneNumber":"...","comments":"..."}`. |

Errors are RFC 9457 problem documents:

| Status | Meaning |
|--------|---------|
| `400` | Bad request — for example a missing location or a non-positive radius. |
| `409` | The report was refused by a safety gate (submission disabled, wrong account, daily cap reached). |
| `502` / `504` | Duke Energy was unreachable, timed out, or rejected the map credentials. |
| `503` | The account-scoped flow is not configured. The `detail` field says exactly what is missing. |

`/api/v1/home/status` never fails because one source is down. Each section degrades to `null` and
the reason is appended to the `notes` array, so a partial answer still tells you what it knows:

```json
{
  "label": "Home",
  "account": null,
  "neighborhood": { "outageCount": 2, "customersAffected": 141, "nearestOutageMiles": 0.18, "outages": [ ... ] },
  "county": { "countyName": "Wake", "customersServed": 100000, "customersAffected": 2500, "percentAffected": 2.5 },
  "notes": ["The outage-report flow is disabled. Set DukeEnergy:Report:Enabled to true to enable it."],
  "outageIndicated": true
}
```

---

## Configuration

Settings come from the `DukeEnergy` section of the file passed to `--settings`, and from
environment variables using the standard `DukeEnergy__Home__AccountNumber` double-underscore form.
Keep the account number, phone number and any bearer token in the environment rather than in the
file.

```json
{
  "DukeEnergy": {
    "Jurisdiction": "DEC",
    "Home": {
      "Label": "123 Main St",
      "Latitude": 35.7796,
      "Longitude": -78.6382,
      "RadiusMiles": 1.5,
      "County": "Wake",
      "State": "NC"
    },
    "Report": {
      "Enabled": false,
      "AllowSubmit": false,
      "DryRun": true,
      "ProfilePath": "profiles/duke-outage-report.json",
      "MaxSubmissionsPerDay": 5
    }
  }
}
```

| Key | Default | Notes |
|-----|---------|-------|
| `Jurisdiction` | `DEC` | `DEC` Carolinas, `DEF` Florida, `DEI` Indiana, `DEM` Ohio/Kentucky. `DEP` (Progress) is offered but unverified. |
| `Home.RadiusMiles` | `1.0` | What counts as "the neighbourhood". |
| `OutageCacheDuration` | `00:02:00` | Duke refreshes roughly every 15 minutes, so polling faster only adds load. |
| `Report.Enabled` | `false` | Gates account lookup and existing-outage reads. |
| `Report.AllowSubmit` | `false` | Gates filing reports. Required *in addition to* `Report.Enabled`. |
| `Report.DryRun` | `false` | Renders and validates a submission without sending it. Leave on until you have seen the rendered body. |
| `Report.MaxSubmissionsPerDay` | `5` | Hard cap on submissions in a rolling 24 hours. |

### Why filing a report is gated three ways

`POST /api/v1/outages/report` writes to a real utility's operational systems. A duplicate or
incorrect report is not a harmless retry — it can dispatch a crew. So the client will not submit
unless `Report.Enabled` **and** `Report.AllowSubmit` are both set, refuses outright if the request
names an account other than `Home.AccountNumber`, and stops at `MaxSubmissionsPerDay`. Each refusal
is a `409` naming the gate that stopped it.

---

## How the outage-map half authenticates

The outage map ships public client credentials to every browser that loads it. The client:

1. Fetches `https://outagemap.duke-energy.com/config/config.prod.json`.
2. Reads `consumer_key_emp` and `consumer_secret_emp`.
3. Sends `Authorization: Basic base64(key:secret)` to `https://cust-api.duke-energy.com/outage-maps/v1/...`
   along with the `Origin`, `Referer` and `User-Agent` headers the map itself sends.

These identify the map application, not you. They are cached in memory for an hour, never written
to disk or logs, and re-fetched once automatically if the API starts rejecting them.

Response parsing is deliberately tolerant: field names are matched case-insensitively against
several known spellings, the `data` / `results` / GeoJSON `features` envelopes are all unwrapped,
and any field that cannot be found becomes `null` rather than failing the request. Duke has renamed
these fields before.

---

## Filling in the account flow

The account-scoped endpoints are driven by
`src/Iaet.DukeEnergy/profiles/duke-outage-report.template.json`, which ships with `REPLACE_ME` in
every unknown position. Capture the real values with IAET, using **your own account**.

### 1. Create a project and capture the flow

```bash
iaet project create --name duke-outage \
  --url "https://outagereport.duke-energy.com/#/report-outage/home/find-account/phone-number/usecase/existing-outage" \
  --auth-required

# Load extensions/iaet-capture/dist in Chrome, click Start, then in the tab:
#   1. enter your phone number and submit the account lookup
#   2. let the page show your existing outage status
#   3. if you are genuinely out, walk the report form up to (not through) the final submit
# Click Stop, then Export.

iaet import --file capture.iaet.json --project duke-outage
```

Walking the form without submitting still captures the submit request's URL, headers and body
shape from the app's own JavaScript. Only submit for real when you actually have an outage.

### 2. Read off the requests

```bash
iaet catalog sessions
iaet catalog endpoints --session-id <guid>
iaet export openapi --session-id <guid> --project duke-outage
iaet explore --db catalog.db --projects .iaet-projects   # dashboard, if you prefer clicking
```

### 3. Copy the template and fill it in

```bash
mkdir -p profiles
cp src/Iaet.DukeEnergy/profiles/duke-outage-report.template.json profiles/duke-outage-report.json
```

Fill in `baseUri`, each `urlTemplate`, each `body`, and each `responseMap` path. A profile looks
like this once complete:

```json
{
  "baseUri": "https://cust-api.duke-energy.com",
  "defaultHeaders": {
    "Accept": "application/json, text/plain, */*",
    "Origin": "https://outagereport.duke-energy.com",
    "Authorization": "{{env:DUKE_OUTAGE_REPORT_AUTH}}"
  },
  "lookupAccount": {
    "method": "POST",
    "urlTemplate": "/outage-report/v1/accounts/search",
    "body": "{\"phoneNumber\":\"{{phoneNumber}}\"}",
    "responseMap": {
      "accountNumber": "data.accounts[0].accountNumber",
      "serviceAddress": "data.accounts[0].serviceAddress.line1"
    }
  }
}
```

Template mechanics:

- `{{phoneNumber}}`, `{{accountNumber}}`, `{{comments}}`, `{{email}}` are substituted by the client.
  Values are percent-encoded in URLs and JSON-escaped in bodies, so a comment containing quotes
  cannot break the payload.
- `{{env:NAME}}` reads an environment variable. **Put every credential behind this** — a captured
  bearer token belongs in the environment, not in a file you might commit.
- `responseMap` values are dotted paths with array indexers: `data.accounts[0].accountNumber`.
- An unknown token renders as an empty string rather than failing, so a partly filled profile still
  makes a request you can inspect.

Well-known `responseMap` keys the client interprets:

| Template | Keys |
|----------|------|
| `lookupAccount` | `found`, `accountNumber`, `serviceAddress` |
| `existingOutage` | `hasActiveOutage`, `outageId`, `status`, `cause`, `reportedAt`, `estimatedRestorationAt` |
| `submitReport` | `accepted`, `confirmationNumber`, `message` |

`found` and `hasActiveOutage` are inferred from whether an account number or outage id came back
when you do not map them. Timestamps accept ISO-8601 or Unix epoch, in seconds or milliseconds.

### 4. Enable it, dry-run first

```json
"Report": { "Enabled": true, "AllowSubmit": true, "DryRun": true, "ProfilePath": "profiles/duke-outage-report.json" }
```

With `DryRun` on, `POST /api/v1/outages/report` renders and validates the request and returns a
receipt with `"dryRun": true` without sending anything. Confirm the rendered body matches what the
browser sent, then turn `DryRun` off.

---

## Keeping it working

Duke Energy can change these endpoints without notice, and nothing here is a contract. Two failure
modes and their fixes:

- **`502` with "could not be authenticated"** — the configuration document moved its credential
  fields. Re-capture the outage map and check `config.prod.json`.
- **`200` with `null` fields, or an empty neighbourhood during a real outage** — the response field
  names changed. Add the new spelling to `OutageJsonParser`, or re-capture and update the profile's
  `responseMap`.

Both cases are visible before they are silent, because the models keep every extracted field in
`fields` and the home status keeps its `notes`.

---

## Scope and etiquette

- Use it against **your own account and your own service address**. Account lookup by phone number
  will resolve other people's accounts; that is not what this is for.
- Responses are cached and requests are rate-limited on purpose. Do not lower those to poll harder —
  the upstream data only changes every 15 minutes or so.
- Reporting an outage dispatches real work. Report outages you actually have.
- This is an unofficial client built by reading traffic the browser already sends. It is not
  affiliated with or endorsed by Duke Energy, and Duke's terms of use apply to your use of it.
