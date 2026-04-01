# BLE Client Generation Request — Raddy Radio-C

Generate a complete, production-ready **C#** BLE client for controlling a Raddy Radio-C shortwave radio over Bluetooth Low Energy.

## Device Information

- **App Package**: `com.myhomesmartlife.bluetooth` v1.2.0
- **BLE Library Used**: `cn.com.heaton.blelibrary`
- **Transport**: L2CAP dynamic channels (NOT standard GATT/ATT)
- **Protocol**: Custom binary protocol with `0xAB` header

## BLE Connection

### Services and Characteristics

| UUID | Name | Operations |
|------|------|------------|
| `0000FEE9-0000-1000-8000-00805F9B34FB` | Primary Service | — |
| `d44bc439-abfd-45a2-b575-925416129600` | Write Characteristic | Write |
| `d44bc439-abfd-45a2-b575-925416129601` | Notify Characteristic | Notify |

### Connection Sequence

1. Scan for BLE devices
2. Connect to device with GATT
3. Discover services → find service `FEE9`
4. Get write characteristic (`...9600`) and notify characteristic (`...9601`)
5. Enable notifications on notify characteristic (write CCCD `0x0001`)
6. Send handshake: `[0xAB, 0x01, 0xFF, 0xAB]`
7. Wait for handshake response
8. Device is ready for commands

## Protocol Format

### Command Frame (App → Radio)

```
[0xAB] [Length] [CommandGroup] [CommandId] [Checksum]
```

- **Header**: Always `0xAB`
- **Length**: Number of bytes following (usually `0x02`)
- **CommandGroup**: `0x0C` for button/control commands, `0x12` for acknowledgments
- **CommandId**: Identifies the specific command (0-73)
- **Checksum**: `(0xAB + Length + CommandGroup + CommandId) & 0xFF`

### Response Frame (Radio → App)

```
[0xAB] [Length] [ResponseGroup] [Payload...] [Checksum]
```

Responses are variable-length. The ResponseGroup byte identifies the type.

## Complete Command Table

### Button/Control Commands (Group 0x0C)

| Command ID | Name | Description |
|-----------|------|-------------|
| 0 | Band | Cycle through radio bands (FM/AM/SW/LW) |
| 1-9 | Number 1-9 | Keypad digits |
| 10 | Number 0 | Keypad zero |
| 11 | Backspace | Delete last frequency digit |
| 12 | Decimal Point | Insert decimal point |
| 13 | Freq Enter | Confirm frequency input |
| 14 | Tune Up (short) | Step up one frequency increment |
| 15 | Tune Up (long) | Fast scan up |
| 16 | Tune Down (short) | Step down one frequency increment |
| 17 | Tune Down (long) | Fast scan down |
| 18 | Volume Up | Increase volume |
| 19 | Volume Down | Decrease volume |
| 20 | Power | Toggle power on/off |
| 23 | Sub-Band | Sub-band selection |
| 26 | Play/Pause | Toggle playback |
| 27 | Step | Change tuning step size |
| 28 | Bluetooth Audio | Switch to BT audio mode |
| 29 | Demodulation | Cycle: WFM→NFM→MONO→STEREO→AM→SSB→LSB→USB |
| 30 | Bandwidth | Change filter bandwidth |
| 31 | Mobile Display | Toggle mobile display mode |
| 32 | Squelch (SQ) | Open squelch control |
| 33 | Stereo | Toggle stereo/mono |
| 34 | De-emphasis | De-emphasis setting |
| 35 | Preset | Recall preset station |
| 36 | Memo | Save current station to memory |
| 37 | Rec | Start/stop recording |
| 38 | Music | Switch to music mode |
| 39 | Circle | Repeat mode cycle |
| 40 | Music Circle | Music repeat mode |
| 41 | Band (long press) | Extended band selection |
| 42 | SOS (short) | Emergency SOS |
| 43 | SOS (long) | Extended SOS |
| 45 | REC click | Recording toggle |

### Acknowledgment Commands (Group 0x12)

| Bytes | Meaning |
|-------|---------|
| `AB 02 12 01 C0` | Accept success |
| `AB 02 12 00 BF` | Accept failure |

## Complete Response Protocol (54 handlers)

### Frequency & Band Status (code `ab0901`)
- **Byte 3**: Signal strength level (0-6, maps to S-meter bars)
- **Byte 4**: Decimal point position for frequency display
- **Bytes 5-8**: Current frequency (hex string, variable length per band)
- **Byte 9+**: Band code and unit text

**Band Codes**: FM=0, MW=1, LW=2, SW1-SW18=3-20, NOAA/WB/AIR/CB/VHF/UHF/OTHER

### Volume Level (code `ab0209`)
- **Byte 3**: Current volume (0-63, displayed as "VOL:N")

### Battery Level (code `ab0308`)
- **Byte 3**: Battery bars (0-4)
- **Byte 4**: Charging status (`01`=charging)

### Demodulation & Settings (code `ab0904`)
- **Byte 3**: Demodulation mode (0=WFM, 1=NFM, 2=MONO, 3=STEREO, 4=AM, 5=SSB, 6=LSB, 7=USB)
- **Byte 4**: Filter/bandwidth setting
- **Byte 5-6**: Step value (hex)

### Device Mode (code `ab0207`)
- **Byte 3**: Mode (0=Radio, 1=Bluetooth, 2=SD Card, 3=AUX, 4=PC/USB)

### Signal Strength (code `ab0303`)
- **Byte 3**: RSSI value
- **Byte 4**: SNR value

### Sleep Timer (code `ab0205`)
- **Byte 3**: Minutes remaining

### EQ Mode (code `ab020a`)
- **Byte 3**: Equalizer preset (0=Normal, 1=Rock, 2=Pop, 3=Classic, 4=Jazz, 5=Bass)

### Playback Status (code `ab020e`)
- **Byte 3**: Status (`01`=playing, `00`=paused)

### Playback Time (code `ab070d`)
- **Bytes 3-4**: Elapsed minutes
- **Bytes 5-6**: Elapsed seconds
- **Bytes 7-8**: Track index

### Track Count (code `ab060c`)
- **Bytes 3-4**: Current track number
- **Bytes 5-6**: Total track count

### Loop Mode (code `ab0211`)
- **Byte 3**: Mode (0=repeat all, 1=repeat one, 2=folder, 3=shuffle)

### UI Button State (code `ab0417`)
- **Byte 3**: SubBand button (00=disabled/gray, 01=enabled)
- **Byte 4**: Step button state
- **Byte 5**: Mode/Scan button state

### Power Off (exact match `ab031300c0`)
- Device has powered off, show reconnect prompt

### Multi-Packet String Protocol
Long strings (device name, labels) use 4-packet assembly:
1. Subcommand `01`: First packet → start buffer
2. Subcommand `02`: Middle packet → append
3. Subcommand `03`: Final packet → append + decode
4. Encoding: GBK (Chinese character set)

### Squelch (code `ab0318`)
- **Byte 3**: Current SQ level
- **Byte 4**: Maximum SQ level

### Band Bitmask (code `ab0315`)
- **Bytes 3-4**: 16-bit bitmask of enabled bands

### BLE Disconnect (code `ab0224`)
- Radio requests disconnect — close GATT connection

## Frequency Encoding/Decoding (VERIFIED)

### Decoding frequency from `ab0901` response

The frequency is encoded in Bytes 4-7 using **nibble extraction**:

```csharp
// Extract nibbles from bytes 4-7
byte b4High = (byte)((byte4 >> 4) & 0x0F);
byte b4Low  = (byte)(byte4 & 0x0F);
byte b5High = (byte)((byte5 >> 4) & 0x0F);
byte b5Low  = (byte)(byte5 & 0x0F);
byte b6Low  = (byte)(byte6 & 0x0F);

// Reassemble as hex string: B6L | B5H | B5L | B4H | B4L
string freqHex = $"{b6Low:X}{b5High:X}{b5Low:X}{b4High:X}{b4Low:X}";
uint freqRaw = Convert.ToUInt32(freqHex, 16);

// Apply decimal places based on band
int decimalPlaces = bandCode switch {
    0x00 => 2,   // FM: 102.30 MHz (÷100)
    0x01 => 0,   // MW: 1270 KHz (÷1)
    _    => 3    // SW/AIR/VHF/WB: 119.345 MHz (÷1000)
};
double freq = freqRaw / Math.Pow(10, decimalPlaces);
```

### Band codes (Byte 3 of `ab0901`)

| Code | Band | Unit | Decimal Places | Example |
|------|------|------|----------------|---------|
| 0x00 | FM | MHz | 2 | 102.30 |
| 0x01 | MW | KHz | 0 | 1270 |
| 0x02 | SW | MHz | 3 | 6.175 |
| 0x03 | AIR | MHz | 3 | 119.345 |
| 0x06 | WB | MHz | 3 | 162.550 |
| 0x07 | VHF | MHz | 3 | 145.800 |

### Byte 8: Unit indicator
- `0x00` = MHz
- `0x01` = KHz

### Byte 9: Signal strength (nibble-packed)
- High nibble (bits 7-4): Signal strength level (0-6)
- Low nibble (bits 3-0): Signal bars/mode indicator

### Sending a frequency (digit-by-digit)

There is NO single "set frequency" command. Frequency entry works like a physical keypad:

```
1. Send sendButtonFreq (AB 02 0C 0D) → enter frequency input mode
2. Send digits one by one:
   - sendNumber1 (AB 02 0C 01) for "1"
   - sendNumber0 (AB 02 0C 0A) for "0"
   - sendNumber2 (AB 02 0C 02) for "2"
   - sendButtonPoint (AB 02 0C 0C) for "."
   - sendNumber3 (AB 02 0C 03) for "3"
3. Send sendButtonFreq (AB 02 0C 0D) → confirm frequency
```

A higher-level `SetFrequency(double freq)` method should:
1. Convert frequency to string
2. Send freq_enter to start input mode
3. Send each digit/decimal as individual commands with ~50ms delay between
4. Send freq_enter to confirm

## Client Requirements

- Language: **C#**
- Use `Plugin.BLE` or `InTheHand.BluetoothLE` for cross-platform BLE
- Implement all 73 commands with strongly-typed methods
- **High-level methods**: `SetFrequency(double freq)` (digit-by-digit), `SetBand(RadioBand band)`, `TuneUp()`, `TuneDown()`, `VolumeUp()`, `VolumeDown()`, `PowerToggle()`
- Parse all 54 response types into strongly-typed events/callbacks
- **Frequency decoding**: Implement the nibble extraction formula above for parsing `ab0901` responses
- Include event-driven architecture: `OnFrequencyChanged`, `OnVolumeChanged`, `OnBatteryChanged`, `OnModeChanged`, `OnSignalStrengthChanged`, `OnDemodulationChanged`, `OnPlaybackChanged`
- **RadioState model**: Properties for Frequency, Band, Volume, Battery, SignalStrength, DemodMode, EqMode, DeviceMode, IsPlaying, SqLevel, SleepTimer
- Handle connection lifecycle: scan → connect → discover services → enable notifications → handshake → ready
- Handle multi-packet string assembly with GBK decoding
- Include checksum calculation: `checksum = (sum of all bytes except checksum) & 0xFF`
- Include reconnection logic with exponential backoff
- Thread-safe command queue (BLE write operations must be serialized)
- XML doc comments on all public members

Generate the complete client code now.
