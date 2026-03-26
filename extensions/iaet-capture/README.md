# iaet-capture

A Chrome extension that captures API traffic without DevTools open, by injecting fetch/XMLHttpRequest interceptors into every page.

## Features

- Start/stop recording from the browser popup
- Background capture of all fetch/XHR requests (no DevTools needed)
- Badge count showing the number of unique endpoint signatures seen
- Export as `.iaet.json` file
- Can POST directly to an `iaet import --listen` server

## Build

```bash
npm install
npm run build
# Output in dist/
```

## Load in Chrome

1. Open Chrome → `chrome://extensions`
2. Enable **Developer mode**
3. Click **Load unpacked** → select the `dist/` folder (after `npm run build`)
4. The IAET Capture icon appears in the toolbar

## Usage

1. Click the IAET Capture icon in the toolbar
2. Enter a session name
3. Click **Start** — the badge turns blue and starts counting unique endpoints
4. Browse the target application
5. Click **Stop** when done
6. Click **Export .iaet.json** to download the capture file

## POST to IAET CLI Listener

```bash
# Terminal 1 — start the listener
iaet import --listen --port 7474

# Browser — enter http://localhost:7474/import in the POST field and click POST
```

## Import captured file

```bash
iaet import --file capture.iaet.json
```

## Architecture

| File | Role |
|------|------|
| `background.ts` | Service worker — stores requests, manages state, handles badge |
| `content.ts` | Content script — injects `inject.js` into the page main world |
| `inject.ts` | Injected script — monkey-patches `fetch` and `XMLHttpRequest` |
| `popup.ts` | Popup UI — start/stop/export controls |
| `types.ts` | Shared types (`.iaet.json` format + message protocol) |
