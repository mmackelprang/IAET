# Investigation: Google Voice SIP WebSocket Upgrade Headers

**Requested by:** GVResearch project (D:/prj/GVResearch)
**Priority:** BLOCKING — this is the last piece needed for headless VoIP calls
**Goal:** Capture the exact HTTP/1.1 WebSocket upgrade request that the browser sends to Google's SIP proxy at `216.239.36.145:443`.

---

## The Problem

We can establish TCP + TLS to `216.239.36.145:443` successfully (cert CN: `telephony.goog`, TLS 1.2). But when we send a WebSocket upgrade request, the server **silently ignores it** — no response, no rejection, just silence until timeout.

The browser (Chrome) successfully connects and exchanges SIP messages over this WebSocket. We need to see exactly what the browser's WebSocket upgrade request looks like.

## What We've Tried (All Failed)

```
GET / HTTP/1.1
Host: telephony.goog
Upgrade: websocket
Connection: Upgrade
Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==
Sec-WebSocket-Protocol: sip
Sec-WebSocket-Version: 13
Origin: https://voice.google.com
```

Result: Server accepts TLS, receives the request, sends nothing back.

Variations attempted:
- With/without Origin header
- With/without User-Agent
- IP address vs `telephony.goog` hostname
- HTTP/1.1 forced (server doesn't support HTTP/2)
- TLS 1.2 forced (server's max)

## What We Need Captured

### 1. The WebSocket Upgrade HTTP Request

Using CDP `Network.webSocketWillSendHandshakeRequest` or similar, capture the **exact HTTP headers** the browser sends when opening the WebSocket to `216.239.36.145`. We specifically need:

- **URL path** — is it `GET /` or something like `GET /voice/sip` or `GET /?token=...`?
- **Host header** — `telephony.goog`, `216.239.36.145`, or something else?
- **Cookie header** — does the browser send Google cookies in the WebSocket upgrade?
- **Authorization header** — is there a SAPISIDHASH or Bearer token in the upgrade?
- **All Sec-WebSocket-* headers** — key, protocol, version, extensions
- **Any custom headers** — X-Google-*, X-GV-*, etc.
- **Origin header** — exact value

### 2. The WebSocket Upgrade HTTP Response

The server's `101 Switching Protocols` response:
- **Sec-WebSocket-Accept** header
- **Sec-WebSocket-Protocol** header (should be `sip`)
- Any other headers

### How to Capture

**Method 1: CDP WebSocket events**

```javascript
// In a CDP session connected to Chrome:
cdpSession.send('Network.enable');

// These events fire during WebSocket handshake:
// Network.webSocketWillSendHandshakeRequest — has the upgrade request headers
// Network.webSocketHandshakeResponseReceived — has the 101 response
// Network.webSocketCreated — has the URL
```

**Method 2: Chrome DevTools → Network tab**

1. Open `chrome://flags` and enable "Show WebSocket frames in Network tab" (if not already)
2. Open DevTools → Network → filter by `WS`
3. Navigate to `voice.google.com`
4. The WebSocket connection to `216.239.36.145` should appear
5. Click on it → Headers tab shows the upgrade request/response

**Method 3: IAET capture with WebSocket interception**

```bash
iaet capture start --target "Google Voice" --url https://voice.google.com --session gv-ws-upgrade
```

With WebSocket event capture enabled.

### Chrome Debug Profile

The debug Chrome profile with a logged-in Google session is at:
```
%LOCALAPPDATA%\GvResearch\chrome-debug-profile
```

Or launch Chrome with:
```
chrome --remote-debugging-port=9222
```

Then connect via CDP.

### What the Browser Does (From JS Analysis)

From the TsSIP JS code in the GV web app bundle:

```javascript
// WebSocket creation:
const uri = `sip:${host}${port ? `:${port}` : ""};transport=ws`;
const ws = new WebSocket(this.url, "sip");  // subprotocol: "sip"
ws.binaryType = "arraybuffer";
```

The `this.url` is constructed from the SIP registration info. It likely looks like `wss://216.239.36.145:443` or possibly `wss://216.239.36.145:443/some/path`.

## What's Already Working

Everything up to the WebSocket connection is proven working:

| Component | Status |
|-----------|--------|
| Cookie extraction (CLI tool) | Working — automated via CDP |
| SAPISIDHASH auth | Working — voice API returns 200 |
| sipregisterinfo/get | Working — returns SIP credentials |
| Signaler (BrowserChannel) | Working — connected, events flowing |
| REST API (threads, account, SMS) | Working — verified against live GV |
| TCP + TLS to 216.239.36.145 | Working — TLS 1.2 handshake succeeds |
| WebSocket upgrade | **BLOCKED** — server silently ignores |

## Output

Save the captured WebSocket upgrade headers to:
- `D:/prj/GVResearch/captures/iaet-exports/gv-websocket-upgrade-headers.json`

Format:
```json
{
  "url": "wss://216.239.36.145:443/...",
  "requestHeaders": {
    "Host": "...",
    "Upgrade": "websocket",
    "Connection": "Upgrade",
    "Sec-WebSocket-Key": "...",
    "Sec-WebSocket-Protocol": "sip",
    "Sec-WebSocket-Version": "13",
    "Origin": "...",
    "Cookie": "...",
    "Authorization": "...",
    "...": "..."
  },
  "responseHeaders": {
    "HTTP/1.1": "101 Switching Protocols",
    "Sec-WebSocket-Accept": "...",
    "...": "..."
  }
}
```

Once we have these headers, the GVResearch softphone can replicate them exactly and establish the SIP-over-WebSocket connection for real VoIP calls.
