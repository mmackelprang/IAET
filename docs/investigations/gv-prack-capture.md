# Investigation: Capture Exact PRACK Message Bytes

**Requested by:** GVResearch project (D:/prj/GVResearch)
**Priority:** BLOCKING — PRACK is the last piece for the phone to ring
**Goal:** Capture the exact PRACK SIP message the browser sends after receiving a 183 Session Progress, byte-for-byte.

---

## Context

We have SIP-over-WebSocket working end-to-end:
- WebSocket connects to `wss://web.voice.telephony.goog/websocket` ✅
- SIP REGISTER with Digest auth → 200 OK ✅
- SIP INVITE with SDP → 100 Trying → 183 Session Progress ✅

But our PRACK (acknowledging the 183) is being ignored by the server — it keeps retransmitting the 183 until 504 timeout. The phone never rings.

## What We Need

### 1. The Exact PRACK Message

Capture the raw text/bytes of the PRACK message the browser sends via WebSocket after receiving a 183 Session Progress. We need:

- The complete SIP message including all headers and the empty body
- The exact header order
- The exact RAck header format
- Whether it's sent as a Text or Binary WebSocket frame
- The exact Request-URI (with all parameters)

### 2. The 200 OK Response to PRACK

What does the server respond with after accepting the PRACK?

### 3. The 180 Ringing and its PRACK

After 183 → PRACK → 200 OK(PRACK), the server sends 180 Ringing. Capture:
- The 180 Ringing message
- The PRACK the browser sends for the 180
- The 200 OK response to that PRACK

### What Our PRACK Looks Like (Not Working)

```
PRACK sip:+19193718044@ACZQQLQO...:5060;transport=udp;uri-econt=... SIP/2.0
Via: SIP/2.0/wss b8938b7eb875.invalid;branch=z9hG4bK...;keep
Route: <sip:216.239.36.145:443;lr;transport=wss;uri-econt=...>
Route: <sip:216.239.36.145:443;lr;transport=wss>
From: <sip:AXYJnG2j...@web.c.pbx.voice.sip.google.com>;tag=JYQTZSTQYB
To: <sip:+19193718044@web.c.pbx.voice.sip.google.com>;tag=BIUVAQ2JI5...
Call-ID: 7DC5DCAE-7F0D-40DA-B87A-CFE4BA51CA82
CSeq: 2 PRACK
RAck: 1 1 INVITE
Max-Forwards: 70
Content-Length: 0
```

### What the Captured PRACK Looked Like (Previous Session)

From `gv-websocket-sip-capture.json` Frame 4:
```
PRACK sip:+19193718044@ACZQQLQO3P447...:5060;transport=udp;uri-econt=U35K2... SIP/2.0
Via: SIP/2.0/wss 5puacahp1ftn.invalid;branch=z9hG4bK...;keep
Route: <sip:216.239.36.145:443;lr;transport=wss;uri-econt=...>
Route: <sip:216.239.36.145:443;lr;transport=wss>
Max-Forwards: 69
To: ...;tag=...
From: ...;tag=...
Call-ID: ...
CSeq: 2 PRACK
RAck: 1 6383 INVITE
Content-Length: 0
```

### Possible Differences to Check

1. **Header order** — captured has `Max-Forwards` before `To`/`From`, ours has it after
2. **RAck CSeq number** — captured has `6383`, ours has `1` (our INVITE CSeq is 1)
3. **WebSocket frame type** — is PRACK sent as Text or Binary?
4. **Missing headers** — are there any headers we're not including?
5. **Via branch format** — does it need to match a specific pattern?
6. **CSeq: 2 PRACK** — is CSeq 2 correct or should it be a different number?

## How to Capture

### Method 1: CDP WebSocket Frame Events

```javascript
// Connect to Chrome via CDP
cdpSession.send('Network.enable');

// WebSocket frame events show the exact payload
// Network.webSocketFrameSent — outgoing frames (our PRACK)
// Network.webSocketFrameReceived — incoming frames (183, 200 OK)
```

The frames in `gv-websocket-upgrade-headers.json` already show some frames but we need the PRACK specifically during an active call.

### Method 2: Make a Call and Capture All Frames

1. Connect to Chrome via CDP (debug profile at `%LOCALAPPDATA%\GvResearch\chrome-debug-profile`)
2. Enable WebSocket frame capture
3. Make an outgoing call from the GV web UI
4. Capture ALL WebSocket frames in order (REGISTER, 401, REGISTER+auth, 200, INVITE, 100, 183, **PRACK**, 200(PRACK), 180, **PRACK**, 200(PRACK), 200(INVITE), ACK)

### Important Notes

- The WebSocket is at `wss://web.voice.telephony.goog/websocket`
- Frames are sent as **Text** by the browser (based on TsSIP code)
- Responses come back as **Binary** from the server
- The SIP UA is TsSIP (TypeScript SIP in the GV web app bundle)

## Output

Save to `D:/prj/GVResearch/captures/iaet-exports/gv-prack-frames.json`:

```json
{
  "frames": [
    {
      "index": 1,
      "direction": "OUT",
      "type": "Text|Binary",
      "timestamp": ...,
      "sipMethod": "PRACK",
      "rawPayload": "PRACK sip:... SIP/2.0\r\n..."
    },
    {
      "index": 2,
      "direction": "IN",
      "type": "Text|Binary",
      "timestamp": ...,
      "sipStatus": "200 OK",
      "rawPayload": "SIP/2.0 200 OK\r\n..."
    }
  ]
}
```

Include ALL frames from the INVITE through the ACK so we can see the complete call setup sequence.
