# Iaet.DukeEnergy.Api

A local REST service over `Iaet.DukeEnergy`, for finding and reporting outages at your own home and
in your neighbourhood.

```bash
iaet duke serve --port 9300 --settings dukeenergy.settings.json
```

```csharp
var app = DukeEnergyApiApp.Build(new DukeEnergyApiOptions { Port = 9300, SettingsPath = "dukeenergy.settings.json" });
await app.RunAsync();
```

Binds loopback only unless `ListenOnAllInterfaces` is set: the service holds your account
configuration and can file outage reports.

| Method | Path |
|--------|------|
| `GET`  | `/health` |
| `GET`  | `/api/v1/jurisdictions` |
| `GET`  | `/api/v1/outages?jurisdiction=` |
| `GET`  | `/api/v1/outages/counties?jurisdiction=` |
| `GET`  | `/api/v1/outages/neighborhood?lat=&lon=&radiusMiles=&jurisdiction=` |
| `GET`  | `/api/v1/home/status` |
| `POST` | `/api/v1/accounts/lookup` |
| `GET`  | `/api/v1/accounts/{accountNumber}/outage` |
| `POST` | `/api/v1/outages/report` |

Everything under `/api/v1/outages` reads the public outage map and needs no credentials. The
account-scoped endpoints need an endpoint profile captured with IAET and return `503` with an
explanation until one is supplied.

Failures are RFC 9457 problem documents: `400` bad request, `409` a safety gate refused the report,
`502`/`504` Duke Energy is unreachable or timed out, `503` the account flow is not configured.

See [docs/duke-energy-rest-interface.md](../../docs/duke-energy-rest-interface.md) for
configuration and the capture runbook.
