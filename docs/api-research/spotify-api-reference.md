# Spotify Web Player — Internal API Reference

**Date:** 2026-03-27
**Method:** Playwright MCP (public browsing) + Chrome DevTools HAR (Premium authenticated)
**Account:** Premium
**Requests captured:** 681 total, 18 unique API endpoints

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                 Spotify Audio Streaming Pipeline                  │
│                                                                   │
│  1. AUTH          open.spotify.com/api/token (anon + TOTP)        │
│                   clienttoken.spotify.com/v1/clienttoken           │
│                                                                   │
│  2. DISCOVERY     apresolve.spotify.com (→ spclient + dealer)     │
│                                                                   │
│  3. METADATA      api-partner.spotify.com/pathfinder/v2/query     │
│                   spclient.wg.spotify.com/metadata/4/track/{gid}  │
│                                                                   │
│  4. PLAYBACK      track-playback/v1/devices/{id}/state (PUT)      │
│   STATE           → returns: file manifest + quality tiers        │
│                                                                   │
│  5. MANIFEST      manifests/v9/json/sources/{id}/options/         │
│                   supports_drm → segments, codecs, key_ids        │
│                                                                   │
│  6. CDN RESOLVE   storage-resolve/v2/files/audio/interactive/     │
│                   {format}/{fileId} → 6 signed CDN URLs           │
│                                                                   │
│  7. DRM KEY       widevine-license/v1/audio/license (POST)        │
│                   → Widevine content decryption keys              │
│                                                                   │
│  8. AUDIO FETCH   audio-fa.scdn.co/audio/{fileId}?{hmac}          │
│                   → encrypted CENC MP4/TS segments                │
│                                                                   │
│  9. DECRYPT       Widevine CDM (AES-128-CTR) → raw AAC            │
│                                                                   │
│  10. PLAY         Web Audio API / MediaSource Extensions           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Authentication

### Anonymous Token
```
GET open.spotify.com/api/token?reason=init&productType=web-player&totp=XXXXXX&totpServer=XXXXXX&totpVer=61
```
Returns: `{ accessToken, isAnonymous: true, clientId }`
- TOTP rotates every 30 seconds
- Sufficient for GraphQL metadata queries

### Client Token
```
POST clienttoken.spotify.com/v1/clienttoken
```
Returns: client-level auth for spclient services

### Authenticated (Premium)
- OAuth2 Bearer token in `Authorization` header
- Obtained via Spotify's login flow (OAuth2 PKCE)
- Required for: playback, library, playlists, lyrics

---

## Service Discovery

```
GET apresolve.spotify.com/?type=dealer-g2&type=spclient
```
Returns available server endpoints:
- **spclient**: `gue1-spclient.spotify.com` (EU), `gew4-spclient.spotify.com`, etc.
- **dealer**: WebSocket endpoint for real-time sync (Spotify Connect)

Server selection is geography-based — `gue1` = Google US East 1.

---

## Core API Endpoints

### 1. GraphQL Gateway
```
POST api-partner.spotify.com/pathfinder/v2/query
```
**The single most important endpoint.** All content queries go through this GraphQL gateway:
- Search (artists, tracks, albums, playlists, podcasts)
- Artist pages (bio, top tracks, discography, related)
- Album details (tracklist, credits, release date)
- Playlist contents
- Home page recommendations
- User profile data

Called **40 times** in a single browsing session.

### 2. Track Metadata
```
GET spclient.wg.spotify.com/metadata/4/track/{gid}?market=from_token
```
Returns detailed track info:
- Name, album, artist(s) with GIDs
- Duration, disc/track number, popularity (0-100)
- ISRC (International Standard Recording Code)
- `has_lyrics` flag
- `language_of_performance`
- Label, licensor
- Cover art (3 sizes: 64px, 300px, 640px)
- `original_audio.format`: `AUDIO_FORMAT_STEREO`

### 3. Playback State Machine
```
PUT gue1-spclient.spotify.com/track-playback/v1/devices/{deviceId}/state
```
Request body (JSON):
```json
{
  "seq_num": 6,
  "state_ref": {
    "state_machine_id": "ChT-fLSZJ1YB5...",
    "state_id": "b007ba4bf9f1...",
    "paused": false
  },
  "sub_state": {
    "playback_speed": 1,
    "position": 100271,
    "duration": 190242,
    "media_type": "AUDIO",
    "bitrate": 256000,
    "audio_quality": "VERY_HIGH",
    "format": 11,
    "is_video_on": false
  },
  "previous_position": 100271,
  "debug_source": "resume"
}
```

Response: **base64-encoded JSON** containing the full state machine:
- Current + upcoming tracks with metadata
- **File manifest per track** with multiple quality tiers
- Each tier: `file_id`, `bitrate`, `audio_quality`, `format`, `gain_db`, `hifi_status`

### 4. Audio Manifest (Adaptive Streaming)
```
GET gue1-spclient.spotify.com/manifests/v9/json/sources/{sourceId}/options/supports_drm
```
Returns DASH-like manifest:
- 4-second segment length
- Multiple profiles (audio + video canvas)
- Widevine DRM key IDs per profile
- Codec info: `mp4a.40.2` (AAC), `avc1.4d4015` (H.264 for canvas videos)

### 5. CDN URL Resolution
```
GET gue1-spclient.spotify.com/storage-resolve/v2/files/audio/interactive/{format}/{fileId}
```
Response:
```json
{
  "result": "CDN",
  "cdnurl": [
    "https://audio-fa-tls130.spotifycdn.com/audio/{fileId}?{hmac_signature}",
    "https://audio-ak.spotifycdn.com/audio/{fileId}?__token__=exp=...~hmac=...",
    "https://audio-fa-quic0.spotifycdn.com/audio/{fileId}?{signature}",
    "https://audio-fa-quic.spotifycdn.com/audio/{fileId}?{signature}",
    "https://audio-fa-tls13.spotifycdn.com/audio/{fileId}?{signature}",
    "https://audio-fa.scdn.co/audio/{fileId}?{signature}"
  ],
  "fileid": "{fileId}",
  "ttl": 86400
}
```
- 6 CDN mirrors with signed URLs
- TTL: 86,400 seconds (24 hours)
- Supports: TLS 1.3, QUIC (HTTP/3), legacy TLS

### 6. Widevine DRM License
```
POST gue1-spclient.spotify.com/widevine-license/v1/audio/license
```
- Request: Widevine challenge (binary protobuf from CDM)
- Response: License with AES-128 content decryption keys
- Required for all audio playback

### 7. Lyrics (Line-Synced)
```
GET spclient.wg.spotify.com/color-lyrics/v2/track/{trackId}/image/{coverArtUrl}
```
Response:
```json
{
  "lyrics": {
    "syncType": "LINE_SYNCED",
    "language": "en",
    "lines": [
      { "startTimeMs": "7590", "words": "Oh, my God" },
      { "startTimeMs": "10370", "words": "Whoa" }
    ]
  },
  "colors": { "background": -12345678, "text": -1, ... }
}
```

### 8. Library
```
POST spclient.wg.spotify.com/collection/v2/contains
```
Check if tracks are in user's library (liked songs).

### 9. Playback Telemetry
```
POST gue1-spclient.spotify.com/melody/v1/msg/batch
```
Reports playback events:
```json
{
  "messages": [{
    "type": "jssdk_playback_start",
    "message": {
      "play_track": "spotify:track:5qtwzv99vOr5UTwnTixn7j",
      "file_id": "7829259bc867ef...",
      "playback_id": "b007ba4bf9f1...",
      "ms_start_position": 100271,
      "initially_paused": true
    }
  }],
  "sdk_id": "harmony:4.69.0",
  "platform": "web_player windows 10;chrome 146.0.0.0;desktop"
}
```

### 10. Server Time Sync
```
GET gue1-spclient.spotify.com/melody/v1/time
```

### 11. Remote Config
```
POST gue1-spclient.spotify.com/remote-config-resolver/v3/unauth/configuration
```
Feature flags and A/B test configuration.

---

## Audio Quality Tiers (Premium)

| Quality Label | Bitrate | Format Code | Codec | Container |
|---|---|---|---|---|
| `VERY_HIGH` | 256,000 | 13 | AAC (mp4a.40.2) | TS |
| `HIGH` | 128,000 | 12 | AAC (mp4a.40.2) | TS |
| `NORMAL` | 96,000 | (inferred) | AAC | TS |
| Manifest audio | 160,000 | 4 | AAC (mp4a.40.2) | MP4 |

Each file includes `gain_db` for ReplayGain normalization (e.g., `-6.14 dB`, `-8.63 dB`).

---

## Widevine DRM — Deep Dive

### How It Works in Spotify

1. **Manifest** provides `key_id` per profile (base64: `"sFrYRYb8h1vzLMlqVnaQww=="`)
2. **EME API** in browser creates a `MediaKeySession`
3. Browser generates a **Widevine challenge** (protobuf containing device certificate)
4. Challenge sent to `widevine-license/v1/audio/license`
5. Server returns **license** containing encrypted content keys
6. CDM decrypts the keys using device private key
7. Content keys used to **AES-128-CTR decrypt** each audio segment
8. Decrypted audio fed to `MediaSource` → `AudioContext` → speakers

### Encryption: CENC (Common Encryption)

- Standard: ISO 23001-7 (CENC)
- Encryption: AES-128-CTR (counter mode)
- Each segment encrypted with same content key
- Key rotation: per-track (new license per track change)

### Options for Widevine Decryption Outside a Browser

#### Option 1: Playwright with System Chrome (Recommended)

Use Playwright's `channel` option to launch the user's installed Chrome, which includes Widevine:

```csharp
// C# / .NET
var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Channel = "chrome"  // uses installed Google Chrome
});
```

```javascript
// Node.js
const browser = await chromium.launch({ channel: 'chrome' });
```

```python
# Python
browser = await playwright.chromium.launch(channel="chrome")
```

**Pros:** Fully legal, Widevine works natively, headless mode supported
**Cons:** Requires Chrome installed, still runs a browser process
**Playwright MCP config:** `"args": ["@playwright/mcp@latest", "--browser-channel", "chrome"]`
**Note:** The MCP plugin may not honor this flag — may need Puppeteer or direct Playwright script instead.

#### Option 2: Puppeteer with System Chrome

Unlike Playwright MCP, Puppeteer reliably uses the system Chrome:

```javascript
const puppeteer = require('puppeteer');
const browser = await puppeteer.launch({
    executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
    headless: 'new',
    args: ['--autoplay-policy=no-user-gesture-required']
});
```

**Pros:** Guaranteed Widevine, proven approach, headless
**Cons:** Node.js dependency, separate from .NET stack

#### Option 3: CEF (Chromium Embedded Framework)

Embed Chromium with Widevine in a .NET application:

```csharp
// Using CefSharp
var settings = new CefSettings();
settings.CefCommandLineArgs.Add("enable-widevine-cdm");
Cef.Initialize(settings);
```

**Pros:** Native .NET integration, no separate browser process, full control
**Cons:** Heavy dependency (~200MB), complex setup

#### Option 4: FFmpeg + Widevine CDM Library

Load the Widevine CDM (`widevinecdm.dll`) directly and decrypt:

1. Extract `widevinecdm.dll` from Chrome installation:
   ```
   C:\Program Files\Google\Chrome\Application\{version}\WidevineCdm\_platform_specific\win_x64\widevinecdm.dll
   ```
2. Use the CDM API (C interface):
   - `InitializeCdmModule_4()` → `CreateCdmInstance()`
   - `cdm->CreateSessionAndGenerateRequest()` → get challenge
   - Send challenge to Spotify's license server
   - `cdm->UpdateSession()` with license response → keys loaded
   - `cdm->Decrypt()` on each encrypted sample
3. Feed decrypted AAC to FFmpeg for playback/conversion

**Pros:** No browser needed, minimal footprint, full audio pipeline control
**Cons:** Complex C interop, Widevine CDM is closed-source, legal gray area (DMCA 1201)

#### Option 5: pywidevine (Python, Research)

Open-source Widevine L3 protocol implementation:

```python
from pywidevine import PSSH, Cdm, Device

# Load a CDM device (L3 client cert + private key)
device = Device.load("device.wvd")
cdm = Cdm.from_device(device)

# Create session and get challenge
session_id = cdm.open()
challenge = cdm.get_license_challenge(session_id, pssh)

# Send to Spotify's license server
license_response = requests.post(
    "https://gue1-spclient.spotify.com/widevine-license/v1/audio/license",
    data=challenge,
    headers={"Authorization": f"Bearer {token}"}
)

# Parse keys
cdm.parse_license(session_id, license_response.content)
keys = cdm.get_keys(session_id)
# keys[0].key → AES-128 content key

# Decrypt with mp4decrypt
# mp4decrypt --key {kid}:{key} encrypted.mp4 decrypted.mp4
```

**Pros:** Pure Python, well-documented, active community
**Cons:** Requires L3 device file (not freely available), likely DMCA violation, Spotify ToS violation

#### Option 6: Android Emulator

Run the Spotify Android app, which handles Widevine natively:

1. Set up Android emulator with Google Play Services
2. Install Spotify APK
3. Audio output can be captured from the emulator's virtual audio device
4. Or use Frida to hook the audio pipeline and extract decrypted PCM

**Pros:** Full app functionality, legitimate usage
**Cons:** Heavy setup, not headless-friendly, complex audio capture

### Recommendation Matrix

| Approach | Legality | Complexity | Headless | Audio Control |
|---|---|---|---|---|
| **Puppeteer + Chrome** | Legal | Low | Yes | Via Web Audio API |
| **CEF (.NET)** | Legal | Medium | Yes | Full |
| Playwright + Chrome channel | Legal | Low | Yes (if it works) | Via Web Audio API |
| FFmpeg + CDM library | Gray area | High | Yes | Full |
| pywidevine | Likely illegal | Medium | Yes | Full |
| Android emulator | Legal | High | Partial | Limited |

**For research/educational purposes:** Puppeteer + Chrome is the clear winner.
**For a .NET application:** CEF with Widevine is the production-grade path.
**For maximum audio control:** FFmpeg + CDM gives the deepest access but carries legal risk.

---

## CDN Architecture

| Domain | Protocol | Purpose |
|---|---|---|
| `audio-fa-tls130.spotifycdn.com` | TLS 1.3 | Primary audio CDN |
| `audio-ak.spotifycdn.com` | TLS | Akamai-backed CDN |
| `audio-fa-quic0.spotifycdn.com` | QUIC/HTTP3 | QUIC-optimized CDN |
| `audio-fa-quic.spotifycdn.com` | QUIC/HTTP3 | QUIC fallback |
| `audio-fa-tls13.spotifycdn.com` | TLS 1.3 | TLS 1.3 fallback |
| `audio-fa.scdn.co` | TLS | Legacy CDN |

All URLs are signed with HMAC tokens, valid for 24 hours.

---

## Spotify Connect (Device Sync)

- **Device registration:** Each browser/app instance gets a unique `deviceId`
- **State machine:** Playback managed as a state machine with `seq_num` for ordering
- **WebSocket dealer:** `wss://dealer.spotify.com/` for real-time sync
- **State updates:** `PUT track-playback/v1/devices/{id}/state` on every play/pause/skip
- **`debug_source` values:** `resume`, `before_track_load`, `speed_changed`, `seek`
