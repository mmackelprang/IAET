# Investigation: Google Voice Call Signaling Protocol

**Requested by:** GVResearch project (D:/prj/GVResearch)
**Goal:** Capture the exact protocol used to initiate and receive VoIP calls through the Google Voice web app, including WebRTC SDP exchange, signaler channel messages, and audio stream setup.

---

## Background

The GVResearch project has successfully reverse-engineered Google Voice's REST API (threads, SMS, account) and the signaler push channel (long-poll session setup). However, the **call initiation protocol** remains unknown. When a user clicks "Call" in the GV web app:

- No separate HTTP API call is made for call initiation
- The SDP offer/answer exchange happens through the **signaler WebChannel** (a streaming long-poll response)
- Standard HTTP interception can't capture the streaming response chunks

We need IAET to capture:
1. The signaler channel messages during an outgoing call (SDP offer sent, SDP answer received)
2. The signaler channel messages during an incoming call
3. The WebRTC `RTCPeerConnection` API calls (createOffer, setLocalDescription, setRemoteDescription, etc.)
4. The exact message format used to send an SDP offer through the signaler channel

## What We Already Know

### Signaler Setup (captured and working)
- `POST signaler-pa.clients6.google.com/punctual/v1/chooseServer?key={apiKey}`
  - Request body: `[[null,null,null,[9,5],null,[null,[null,1],[[["1"]]]]],null,null,0,0]`
  - Content-Type: `application/json+protobuf`
  - Response: `["<gsessionid>",3,null,"<ts1>","<ts2>"]`

- `POST .../punctual/multi-watch/channel?VER=8&gsessionid={gsid}&key={apiKey}&RID={n}&CVER=22&zx={rand}&t=1`
  - Form-encoded body with `req0___data__=...` subscription registrations
  - Response (length-prefixed): `51\n[[0,["c","<SID>","",8,14,30000]]]\n`

- `GET .../punctual/multi-watch/channel?...&RID=rpc&SID={sid}&AID=0&CI=0&TYPE=xmlhttp&zx={rand}&t=1`
  - Long-poll that blocks until events arrive

### Auth (working)
- SAPISIDHASH: `SAPISIDHASH <timestamp>_<sha1(timestamp + " " + SAPISID + " " + origin)>`
- Cookies: Full browser cookie set required (SIDCC, __Secure-*PSIDTS, NID, etc.)
- Origin: `https://voice.google.com`

### WebRTC (from earlier IAET captures)
- STUN: `stun:stun.l.google.com:19302`
- Google's SIP UA: "xavier"
- Media relay: `74.125.39.0/24:26500` (UDP)
- Codecs: Opus/48000/2 (primary), G722, PCMU, PCMA
- Outgoing: ~1.3s to connected
- Incoming: ~260ms, renegotiation at ~6s

## Investigation Steps

### Step 1: Start IAET capture on voice.google.com

```bash
iaet capture start --target "Google Voice" --url https://voice.google.com --session gv-call-signaling
```

Configure to capture:
- All HTTP traffic to `*.clients6.google.com`
- WebSocket/streaming responses (signaler long-poll chunks)
- WebRTC API events (RTCPeerConnection lifecycle)

### Step 2: Make an outgoing call

Once the GV web app loads and the signaler connects:
1. Click on a contact (e.g., +19193718044)
2. Click the phone/call icon
3. Wait for the phone to ring and answer
4. Talk for a few seconds
5. Hang up

### Step 3: Receive an incoming call

1. Call the GV number (+19196706660) from another phone
2. Answer in the web app
3. Talk for a few seconds
4. Hang up

### Step 4: Capture the key data

For each call, we need:

**Signaler channel messages:**
- What messages are sent via POST to the signaler channel during call setup?
- What data arrives in the streaming long-poll response during call setup?
- What's the exact `req___data__` format for sending an SDP offer?
- Is there a call initiation message BEFORE the SDP offer?
- What format does the SDP answer arrive in from the long-poll?

**WebRTC events:**
- `createOffer()` — what SDP is generated?
- `setLocalDescription()` — when is it called relative to the signaler send?
- `setRemoteDescription()` — what SDP is received from Google?
- `onicecandidate` — are ICE candidates trickled or bundled?
- `onconnectionstatechange` — state transition timeline

**Call lifecycle messages:**
- Is there a "dial" or "initiate" message sent before the SDP offer?
- Is there a "ringing" notification received?
- Is there a "hangup" message and what format is it?
- How is the destination phone number communicated to Google?

### Step 5: Export findings

```bash
iaet catalog endpoints --session-id <guid>
iaet export --session-id <guid> --format json --output gv-call-capture.json
```

## Key Questions to Answer

1. **How does the browser tell Google what number to call?** Is it in the SDP offer, in a separate signaler message, or in a voice API HTTP request we haven't seen?

2. **What format are signaler channel messages in?** The `req___data__` format is URL-encoded protobuf-JSON arrays. What's the exact structure for a call initiation message vs an SDP offer vs a hangup?

3. **Is the signaler channel bidirectional?** We know POSTs send data and the long-poll GET receives data. Are the formats symmetric?

4. **What does the long-poll streaming response look like during a call?** The response is length-prefixed chunks — what events are delivered and in what order?

## Output Location

Save captured data to:
- `D:/prj/IAET/captures/gv-call-signaling/` (raw IAET format)
- `D:/prj/GVResearch/captures/gv-call-protocol.json` (exported findings for the GVResearch project)

## Notes for the IAET Agent

- The GV web app is at https://voice.google.com
- You'll need to use Chrome with the debug profile at `%LOCALAPPDATA%/GvResearch/chrome-debug-profile` (already has a logged-in Google session)
- Or launch Chrome with `--remote-debugging-port=9222` and connect
- The signaler is a **streaming HTTP response** — standard request/response capture won't get the chunks. You need streaming/chunked response interception.
- WebRTC internals can also be viewed at `chrome://webrtc-internals/` in the browser during a call
