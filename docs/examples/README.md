# IAET Example Projects

These are synthetic example projects showing the expected structure and data formats for IAET investigations. They demonstrate what a completed investigation looks like without containing any real captured data.

## Web API Investigation (sample-web-project/)

A hypothetical REST API investigation showing:
- `project.json` — project configuration
- `knowledge/endpoints.json` — discovered API endpoints
- `knowledge/dependencies.json` — auth chains and request ordering
- `output/diagrams/auth-flow.mmd` — Mermaid sequence diagram

## BLE Device Investigation (sample-ble-project/)

A hypothetical BLE device investigation showing:
- `project.json` — project configuration (targetType: android)
- `knowledge/bluetooth.json` — BLE services, characteristics, protocol
- `knowledge/permissions.json` — Android manifest permissions

## Using these examples

```bash
# Copy an example as a starting point
cp -r docs/examples/sample-web-project .iaet-projects/my-investigation

# Or create a real project
iaet project create --name my-target --url https://example.com
```
