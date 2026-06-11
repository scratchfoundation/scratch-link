# Serial Keep-Alive Implementation Guide

## Overview

AluxLabs Link's serial transport supports **keep-alive** functionality to prevent device timeout. This is particularly useful for devices like Codetinker that disconnect if no response is received within 1 second.

## Problem Statement

Some hardware devices (e.g., Codetinker with CH340 USB-to-serial) require continuous communication:
- If no packet is received for > 1 second, the device considers the connection lost
- This triggers device-side notifications (e.g., buzzer sound)
- Regular idle periods during normal operation cause unnecessary timeouts

## Solution

The keep-alive mechanism automatically resends the **last transmitted (TX) packet** whenever the link has been TX-idle for a fixed internal feed interval (~300ms), sized well under the device's ~1s RX watchdog. This keeps the device "alive" without interfering with actual communication.

### Key Features

✅ **Automatic resend** — Last sent packet is cached and resent after the link goes TX-idle  
✅ **No interference with active communication** — Every client write (and every resend) restarts the idle budget, so frequent communication keeps keep-alive silent  
✅ **Firmware update safe** — During firmware updates (DFU), frequent writes keep the link below the idle threshold so keep-alive never fires  
✅ **Optional and configurable** — Can be enabled/disabled per connection  

## Usage

### Serial Connection Request

When connecting to a serial device, include the `keepAliveIntervalMs` parameter:

```json
{
  "baudRate": 115200,
  "peripheralType": "codetinker",
  "keepAliveIntervalMs": 33
}
```

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baudRate` | int | Yes | Baud rate (e.g., 115200) |
| `peripheralType` | string | No | Device type identifier (e.g., "codetinker", "connect", "technic") |
| `keepAliveIntervalMs` | int \| null | No | Enables keep-alive and sets the **poll granularity** in ms — how often Link checks whether the link went idle. `null`/omitted = disabled. Use 33ms for Codetinker. The actual resend cadence is a fixed internal ~300ms idle interval; a small poll value just hits that deadline more precisely. |

### Examples

#### Codetinker (with keep-alive)
```json
{
  "baudRate": 115200,
  "peripheralType": "codetinker",
  "keepAliveIntervalMs": 33
}
```

#### Generic device (no keep-alive)
```json
{
  "baudRate": 9600
}
```

#### Other device with custom interval
```json
{
  "baudRate": 57600,
  "peripheralType": "custom",
  "keepAliveIntervalMs": 100
}
```

## How It Works

### 1. Keep-Alive Enabled (poll = 33ms, feed interval = 300ms)
```
Time: 0ms       → Client sends packet A → idle budget reset
Time: 33–270ms  → (polls fire, but <300ms idle) → no resend
Time: ~300ms    → 300ms idle reached → Keep-alive resends packet A → budget reset
Time: ~600ms    → still idle → Keep-alive resends packet A again
Time: 720ms     → Client sends packet B → idle budget reset
Time: ~1020ms   → still idle → Keep-alive resends packet B
...
```

### 2. During Firmware Update (DFU)
```
Time: 0ms     → DFU write #1 → idle budget reset
Time: 10ms    → DFU write #2 → idle budget reset
Time: 20ms    → DFU write #3 → idle budget reset
...
Result: Keep-alive never fires because the link never stays idle for 300ms
```

## Technical Details

### Idle-Budget Behavior

Link tracks the timestamp of the last TX — either a client `write` or a previous keep-alive resend. On each timer poll it resends only if that timestamp is older than the internal feed interval (~300ms).

This means:
- **Idle state** — Keep-alive resends ~300ms after the last TX, then every ~300ms while idle
- **Active state** — During firmware updates or frequent communication, writes keep the last-TX timestamp fresh, so keep-alive is effectively paused
- **No explicit disable needed** — The mechanism self-manages based on activity
- **Resends count as TX** — A forced resend refreshes the same budget, so one resend doesn't unlock the raw poll cadence; feeds stay ~300ms apart

### Architecture

```
SerialSession (abstract)
├── StartKeepAlive(interval)  → Start poll timer at the client interval
├── StopKeepAlive()           → Stop and dispose timer
└── OnKeepAliveTick()         → On each poll, resend cached packet if TX-idle ≥ feed interval

Platform-specific implementation (WinSerialSession, etc.)
├── StartKeepAlive() in DoConnect()
└── StopKeepAlive() in DoDisconnect()
```

## Firmware Update Safety

**Q: Won't keep-alive packets interfere with firmware updates?**

A: There are two layers of protection.

**Layer 1 — Automatic (no client change needed).** During an active DFU burst:
1. Each `write` request refreshes the last-TX timestamp.
2. DFU chunks arrive faster than the ~300ms feed interval, so the idle budget never expires.
3. Keep-alive only re-arms once the burst stops and the link sits idle for the feed interval.

**Layer 2 — Explicit toggle (recommended for wireless DFU).** For setups where the bootloader handshake travels over a slow wireless link (e.g. USB dongle → wireless → CPU), there is a brief idle window between "wake bootloader" and the first DFU command where keep-alive *could* fire. Eliminate it by calling `setKeepAlive` to disable keep-alive before bootloader entry and re-enable it after DFU completes:

```javascript
await link.send("setKeepAlive", { intervalMs: null });
// ... run DFU ...
await link.send("setKeepAlive", { intervalMs: 33 });
```

See [SerialApiReference.md](SerialApiReference.md#setkeepalive) for the full method spec.

## Diagnosing Transport Issues

If you suspect bytes are being dropped, corrupted, or stalled in the Link layer, enable `wireTrace: true` on `connect`:

```json
{
  "baudRate": 115200,
  "peripheralType": "codetinker",
  "keepAliveIntervalMs": 33,
  "wireTrace": true
}
```

Link will emit hex dumps via `Trace.WriteLine` for every TX/RX (including keep-alive resends). View them in [DebugView](https://learn.microsoft.com/sysinternals/downloads/debugview) or an attached debugger. Compare to the client's own message log to pinpoint where data diverges.

## Troubleshooting

### Device still times out
- Verify `keepAliveIntervalMs` is set in the connect request
- Check if the device actually requires keep-alive (some devices don't)
- Try a shorter interval (e.g., 25ms instead of 33ms)

### Excessive resends visible in logs
- This is expected behavior
- Keep-alive packets only resend when there's no other activity
- During normal operation, keep-alive should rarely fire

### Firmware update hangs or fails
- Ensure the device supports the DFU protocol
- Verify connection parameters (baud rate, etc.)
- Check device logs for error messages

## Related Features

### Phase B: Firmware Transport Abstraction
- `FirmwareTransportPort` interface (planned)
- WebSerial adapter (planned)
- WebSocketLink adapter (planned)
- firmware-updater.ts transport-agnostic implementation (planned)

The keep-alive mechanism works transparently with future firmware update features.

## Support

For issues or questions about keep-alive functionality:
1. Check device documentation for timeout requirements
2. Enable debug logging to see keep-alive packets
3. Contact ALUX Labs support

---

**Version**: 1.1  
**Last Updated**: 2026-06-11  
**Affected Devices**: Codetinker, and any device with sub-second timeout requirements
