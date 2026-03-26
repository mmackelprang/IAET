# iaet-devtools

A Chrome DevTools panel extension that captures XHR/Fetch requests in real-time, groups them by normalized endpoint signature, and exports to `.iaet.json` format.

## Features

- Lists XHR/Fetch requests in real-time grouped by normalized endpoint (path IDs replaced with `{id}`)
- Inline tagging — click any request to assign a tag
- Filter presets to hide analytics/telemetry noise (Google Analytics, Segment, Mixpanel, Sentry, etc.)
- **Export to IAET** button generates a `.iaet.json` file compatible with `iaet import`
- Request/response detail view with pretty-printed JSON bodies

## Build

```bash
npm install
npm run build
# Output in dist/
```

## Load in Chrome

1. Open Chrome → `chrome://extensions`
2. Enable **Developer mode**
3. Click **Load unpacked** → select the `dist/` folder (after running `npm run build`) or the `extensions/iaet-devtools/` folder directly (Vite copies `manifest.json` automatically)
4. Open DevTools (F12) → **IAET** tab

## Usage

1. Open the IAET panel in Chrome DevTools
2. Navigate the target application — XHR/Fetch requests appear automatically
3. Click an endpoint group to expand individual requests
4. Click a request to inspect headers and body
5. Add tags via the **Tag** field in the detail pane
6. Toggle **Hide analytics** to filter out telemetry noise
7. Enter a session name and click **Export to IAET** to download a `.iaet.json` file

## Exporting to IAET CLI

```bash
iaet import --file capture.iaet.json
```

Or stream to a running listener:

```bash
iaet import --listen --port 7474 &
# Then in the extension, use POST export
```
