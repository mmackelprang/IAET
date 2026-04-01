# Raddy Radio-C BLE Response Protocol

Reverse-engineered from `com.myhomesmartlife.bluetooth.MainActivity` (decompiled APK, 4711 lines).

## Protocol Overview

- **Transport**: BLE GATT over custom service
- **Write Characteristic**: `0000FF13-0000-1000-8000-00805F9B34FB` (app -> device)
- **Notify Characteristic**: `0000FF14-0000-1000-8000-00805F9B34FB` (device -> app)
- **Header**: All responses begin with `0xAB`
- **Encoding**: Raw bytes converted to lowercase hex string, then parsed by substring offsets
- **String Encoding**: Multi-byte text uses GBK encoding (Chinese character set)
- **Handshake**: `sendWoshou` is sent immediately after BLE service discovery

## Frame Format

```
Byte 0:  0xAB (header, always)
Byte 1:  Length or sub-length (varies by command group)
Byte 2:  Command group byte
Byte 3+: Payload (varies by command)
```

Parsing variables in the decompiled code:
- `substring` = hex chars `[0:6]` (bytes 0-2: header + length + command)
- `substring2` = hex chars `[0:2]` (byte 0: header `0xAB`)
- `substring3` / `str` = hex chars `[4:6]` (byte 2: command group)
- `substring45` = hex chars `[6:8]` (byte 3: subcommand, for variable-format responses)

## Response Codes Summary

### Fixed-Length Responses (matched by first 3 bytes)

| Code | Group | Name | Description | Source Lines |
|------|-------|------|-------------|-------------|
| `ab0417` | 0x17 | UI Button State | Enable/disable STEP, MODE, SCAN buttons | 681-808 |
| `ab031e` | 0x1e | Radio Info Row Visibility | Show/hide/flicker 5 indicator rows | 809-878 |
| `ab0901` | 0x01 | Frequency & Band Status | Main tuning status: band, freq, unit, signal | 879-1007 |
| `ab0303` | 0x03 | Signal Strength | RSSI and SNR numeric values | 1008-1025 |
| `ab031f` | 0x1f | SQ Dialog Control | Dismiss SQ dialog or show on target row | 1026-1059 |
| `ab090f` | 0x0f | Frequency Input Mode | Freq entry status: normal/input/error | 1060-1198 |
| `ab0904` | 0x04 | Demodulation & Settings | Demod mode, filter, step value | 1199-1289 |
| `ab0205` | 0x05 | Sleep Timer | Sleep countdown minutes | 1290-1307 |
| `ab0506` | 0x06 | Alarm Clock | Alarm type, hour, minute, state | 1308-1538 |
| `ab0207` | 0x07 | Device Mode / Source | Radio/BT/SD/AUX/PC mode switch | 1539-1663 |
| `ab0308` | 0x08 | Battery Level | Battery bars + charging status | 1664-1699 |
| `ab0209` | 0x09 | Volume Level | Current volume (VOL:N) | 1700-1708 |
| `ab020a` | 0x0a | EQ Mode | Equalizer preset icon | 1709-1740 |
| `ab0220` | 0x20 | Language Setting | Switch app locale (CN/EN) | 1741-1755 |
| `ab030b` | 0x0b | Device Online Status | Logged only, no UI update | 1756-1758 |
| `ab020e` | 0x0e | Playback Status | Playing/paused icon | 1759-1777 |
| `ab0410` | 0x10 | Preset Station Mode | Preset number + scanning state | 1778-1810 |
| `ab0211` | 0x11 | Loop/Repeat Mode | Repeat all/one/folder/shuffle | 1811-1833 |
| `ab0402` | 0x02 | Track Selection | Track number input mode | 1834-1853 |
| `ab070d` | 0x0d | Playback Time | Elapsed time MM:SS + track index | 1854-1895 |
| `ab060c` | 0x0c | Track Count | Current track / total tracks | 1896-1914 |
| `ab031d` | 0x1d | Radio Detail Row Visibility | Show/hide/flicker 16 text rows | 3045-3246 |
| `ab0224` | 0x24 | BLE Disconnect | Instructs app to disconnect | 3247-3257 |
| `ab0414` | 0x14 | Frequency Range Info | Band config + freq value (no UI) | 3258-3269 |
| `ab0315` | 0x15 | Band Type Bitmask | 16-bit bitmask of enabled bands | 3270-3308 |
| `ab0216` | 0x16 | Hardware Version ID | Device version for VersionDialog | 3309-3321 |
| `ab0318` | 0x18 | SQ Level / Max SQ | Current and max squelch values | 3370-3379 |
| `ab021b` | 0x1b | Unknown Status | Single byte, checked but no UI action | 3380-3391 |
| `ab0322` | 0x22 | SOS/FUNC Flicker | Button label flicker control | 3392-3411 |

### Exact Match

| Code | Name | Description | Source Lines |
|------|------|-------------|-------------|
| `ab031300c0` | Device Power Off | Shows "Please turn on the device..." | 3036-3044 |

### Variable-Format Responses (Multi-Packet Strings)

These use the pattern `header=0xAB`, variable length byte, command group, and a subcommand byte. They implement a multi-packet string assembly protocol for sending text longer than one BLE packet.

#### Multi-Packet Sequence Protocol

| Seq Byte | Meaning |
|----------|---------|
| `01` | First packet -- store in buffer |
| `02` | Continuation -- append to buffer |
| `03` | Final (single packet) -- decode GBK + display |
| `04` | Final (after multi) -- append, decode full buffer, display |

#### Group 0x12 -- Bluetooth Device Name

| Code Pattern | Name | UI Element | Source Lines |
|-------------|------|------------|-------------|
| `ab..12` | BT Device Name | aE (tv_content) | 1915-1961 |

#### Group 0x19 -- Firmware Version

| Code Pattern | Name | UI Element | Source Lines |
|-------------|------|------------|-------------|
| `ab..19` | Firmware Version | P (VersionDialog) | 1962-2022 |

#### Group 0x1c -- Custom Display Labels (16 rows)

| Subcmd | Name | UI Element | Source Lines |
|--------|------|------------|-------------|
| `01` | Demodulation Title | aV (tv_demodulation_title) | 2024-2073 |
| `02` | Demodulation Value | s (tv_demodulation) | 2074-2123 |
| `03` | Bandwidth Title | aW (tv_band_width_title) | 2124-2173 |
| `04` | Bandwidth Value | t (tv_band_width) | 2174-2223 |
| `05` | SNR Title | aX (tv_snr_title) | 2224-2273 |
| `06` | SNR Value | o (tv_snr) | 2274-2323 |
| `07` | RSSI Title | aY (tv_rssi_title) | 2324-2373 |
| `08` | RSSI Value | n (tv_rssi) | 2374-2423 |
| `09` | Row 9 Text | aZ (tv_text9) | 2424-2473 |
| `0a` | Row 10 Text | ba (tv_text10) | 2474-2523 |
| `0b` | Row 11 Text | bb (tv_text_11) | 2524-2573 |
| `0c` | Row 12 Text | bc (tv_text_12) | 2574-2619 |
| `0d` | Volume/SD Label | R (tv_sdvol) | 2620-2665 |
| `0e` | Track Name | D (tv_num) | 2666-2712 |
| `0f` | Secondary Track Info | bO (tv_text_15) | 2713-2759 |
| `10` | REC Status Label | aF (tv_rec) | 2990-3035 |

#### Group 0x21 -- Dynamic Button Labels (5 buttons)

| Subcmd | Name | UI Element | Source Lines |
|--------|------|------------|-------------|
| `01` | MODE Button Label | aM (bt_subband_text) | 2760-2805 |
| `02` | STEP Button Label | aL (bt_step_text) | 2806-2851 |
| `03` | SCAN/Play Button Label | aN (bt_play) | 2852-2897 |
| `04` | SOS Button Label | bW (iv_sos) | 2898-2943 |
| `05` | FUNC Button Label | bX (iv_phone_play) | 2944-2989 |

#### Group 0x23 -- Toast Message

| Code Pattern | Name | UI Element | Source Lines |
|-------------|------|------------|-------------|
| `ab..23` | Toast Message | Android Toast | 3322-3369 |

## Band Codes

| Code | Band | Description |
|------|------|-------------|
| `00` | FM | FM broadcast |
| `01` | AM | AM broadcast |
| `02` | SW | Shortwave |
| `03` | AIR | Aviation band |
| `04` | CB | Citizens Band |
| `05` | UHF | Ultra High Frequency |
| `06` | WB | Weather Band (NOAA) |
| `07` | VHF | Very High Frequency |
| `08` | LW | Longwave |
| `09` | HAM | Amateur radio |
| `10` | RAIL | Railroad communications |
| `11` | MRN | Marine radio |
| `12` | RACE | Racing communications |
| `13` | POL | Police scanner |
| `14` | FIRE | Fire department |
| `15` | OTHER | Other/custom band |

## Demodulation Modes

| Code | Mode | Description |
|------|------|-------------|
| `00` | WFM | Wideband FM |
| `01` | NFM | Narrowband FM |
| `02` | MONO | Mono audio |
| `03` | STEREO | Stereo audio |
| `04` | AM | Amplitude modulation |
| `05` | SSB | Single sideband |
| `06` | LSB | Lower sideband |
| `07` | USB | Upper sideband |

## EQ Presets

| Code | Preset |
|------|--------|
| `00` | Normal |
| `01` | Pop |
| `02` | Rock |
| `03` | Jazz |
| `04` | Classic |
| `05` | Country |

## Device Modes (Source Select)

| Code | Mode | App View |
|------|------|----------|
| `00` | Standby/Off | Tip view ("please enjoy") |
| `01` | Bluetooth Audio | MP3 layout + BT controls |
| `02` | Radio | Radio layout |
| `03` | Sleep/Standby | Tip view ("please enjoy") |
| `04` | SD Card/USB | MP3 layout + track info |
| `06` | PC/USB Audio | MP3 layout + PC icon |
| `07` | AUX Input | MP3 layout + AUX icon |

## Frequency Encoding

Frequency is encoded as a 4-byte little-endian hex value:

```
Bytes: [LSB] [byte1] [byte2] [MSB]
Value: hexToDec(MSB + byte2 + byte1 + LSB)
```

The decimal point position is encoded in a separate byte:
- **Low nibble** (`& 0x0F`): number of digits after decimal point
- **High nibble** (`>> 4 & 0x0F`): number of digits before decimal point

Special case: When band is WB (Weather Band) and frequency bytes are `000000ff`, the display shows "ALERT" with flicker animation instead of a frequency.

## Visibility States

Many responses control UI element visibility with a 3-state enum:

| Value | Meaning | Implementation |
|-------|---------|----------------|
| `00` | Hidden | `setVisibility(INVISIBLE)` + `clearAnimation()` |
| `01` | Visible | `setVisibility(VISIBLE)` + `clearAnimation()` |
| `02` | Flicker | `setVisibility(VISIBLE)` + `flicker()` (250ms alpha animation) |

## Notes

- The checksum for outgoing commands is a simple byte sum: `sum(bytes[0..N-1]) & 0xFF`
- BLE write characteristic UUID `0000FF13` is used for commands (app to device)
- BLE notify characteristic UUID `0000FF14` is used for responses (device to app)
- The app sends `sendWoshou` (handshake) immediately after discovering services
- Frequency bookmarks are stored in Android SharedPreferences keyed by `frequency + band`
- All multi-packet strings use GBK encoding (supports Chinese characters)
- The app supports vibration feedback (100ms) on every button press via `dataSend()`
