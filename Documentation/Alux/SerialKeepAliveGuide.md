# Serial Keep-Alive Implementation Guide

## Overview

AluxLabs Link's serial transport supports **keep-alive** functionality to prevent device timeout. This is particularly useful for devices like Codetinker that disconnect if no response is received within 1 second.

## Problem Statement

Some hardware devices (e.g., Codetinker with CH340 USB-to-serial) require continuous communication:
- If no packet is received for > 1 second, the device considers the connection lost
- This triggers device-side notifications (e.g., buzzer sound)
- Regular idle periods during normal operation cause unnecessary timeouts

## Solution

The keep-alive mechanism automatically resends the **last transmitted (TX) packet** at a configurable interval. This keeps the device "alive" without interfering with actual communication.

### Key Features

✅ **Automatic resend** — Last sent packet is cached and resent periodically  
✅ **No interference with active communication** — Timer resets on every write, so frequent communication automatically pauses keep-alive  
✅ **Firmware update safe** — During firmware updates (DFU), frequent writes prevent keep-alive from firing  
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
| `keepAliveIntervalMs` | int \| null | No | Keep-alive interval in milliseconds. `null` or omitted = disabled. Set to 33ms for Codetinker. |

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

### 1. Keep-Alive Enabled
```
Time: 0ms     → Client sends packet A
Time: 33ms    → (no activity) Keep-alive resends packet A
Time: 66ms    → (no activity) Keep-alive resends packet A
Time: 150ms   → Client sends packet B → Timer resets
Time: 183ms   → (no activity) Keep-alive resends packet B
...
```

### 2. During Firmware Update (DFU)
```
Time: 0ms     → DFU write #1 → Timer resets
Time: 10ms    → DFU write #2 → Timer resets
Time: 20ms    → DFU write #3 → Timer resets
...
Result: Keep-alive never fires because writes are too frequent
```

## Technical Details

### Timer Reset Behavior

The keep-alive timer **resets** whenever:
- A write request is issued (`write` JSON-RPC method)
- A packet is successfully sent

This means:
- **Idle state** — Keep-alive kicks in after `keepAliveIntervalMs` milliseconds of inactivity
- **Active state** — During firmware updates or frequent communication, keep-alive is effectively paused
- **No explicit disable needed** — The mechanism self-manages based on activity

### Architecture

```
SerialSession (abstract)
├── StartKeepAlive(interval)  → Start timer with interval
├── StopKeepAlive()           → Stop and dispose timer
├── ResetKeepAliveTimer()     → Restart timer (called on write)
└── ResendLastData()          → Resend cached packet (called by timer)

Platform-specific implementation (WinSerialSession, etc.)
├── StartKeepAlive() in DoConnect()
└── StopKeepAlive() in DoDisconnect()
```

## Firmware Update Safety

**Q: Won't keep-alive packets interfere with firmware updates?**

A: There are two layers of protection.

**Layer 1 — Automatic (no client change needed).** During an active DFU burst:
1. Each `write` request resets the keep-alive timer.
2. DFU chunks arrive faster than the keep-alive interval, so the timer never expires.
3. The first write after the burst is the only one that re-arms keep-alive.

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

**Version**: 1.0  
**Last Updated**: 2026-05-24  
**Affected Devices**: Codetinker, and any device with sub-second timeout requirements
