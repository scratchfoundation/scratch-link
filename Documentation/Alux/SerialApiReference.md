# Serial Transport API Reference

## JSON-RPC Methods

### discover

Discovers available serial ports.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "discover",
  "params": {
    "filters": [
      {
        "usbVendorId": 6790,
        "usbProductId": 29987,
        "pathHint": "COM"
      }
    ]
  }
}
```

**Parameters:**
- `filters` (array, optional) — Filter discovered ports. If omitted or empty, every enumerated USB serial port is reported. A port is reported when it matches **any** filter (filters are OR'd); within one filter, all specified fields must match (fields are AND'd).

**Filter fields:**
- `usbVendorId` (int, optional) — USB Vendor ID as a decimal integer. Exact match. Omit to skip this check.
- `usbProductId` (int, optional) — USB Product ID as a decimal integer. Exact match. Omit to skip this check.
- `pathHint` (string, optional) — **Case-insensitive substring** match against the OS-level port path (`info.Path`, e.g. `"COM7"` on Windows). Not a prefix, not exact, not a regex. Example: `"COM"` matches `COM3`, `COM12`, etc. Omit to skip this check.

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {}
}
```

**Notifications:** 
Discovered ports arrive as `didDiscoverPeripheral` notifications:
```json
{
  "jsonrpc": "2.0",
  "method": "didDiscoverPeripheral",
  "params": {
    "peripheralId": "port-0",
    "name": "COM7 (CH340)",
    "path": "COM7",
    "vendorId": "0x1a86",
    "productId": "0x7523",
    "rssi": 0
  }
}
```

---

### connect

Opens a serial port connection.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "connect",
  "params": {
    "peripheralId": "port-0",
    "baudRate": 115200,
    "dataBits": 8,
    "parity": "none",
    "stopBits": "one",
    "flowControl": "none",
    "peripheralType": "codetinker",
    "keepAliveIntervalMs": 33,
    "wireTrace": false
  }
}
```

**Parameters:**
- `peripheralId` (string, required) — Port identifier from discovery
- `baudRate` (int, required) — Baud rate (e.g., 9600, 115200)
- `dataBits` (int, optional) — Data bits (default: 8)
- `parity` (string, optional) — "none" | "even" | "odd" | "mark" | "space" (default: "none")
- `stopBits` (string, optional) — "one" | "onePointFive" | "two" (default: "one")
- `flowControl` (string, optional) — "none" | "rtsCts" | "xonXoff" (default: "none")
- `peripheralType` (string, optional) — Device type identifier ("codetinker", "connect", "technic", etc.)
- `keepAliveIntervalMs` (int, optional) — Keep-alive interval in ms. Omit or null to disable. **Recommended: 33ms for Codetinker**
- `wireTrace` (bool, optional) — Diagnostic. When `true`, Link emits per-write/per-read hex dumps via `Trace.WriteLine` (visible in DebugView or attached debugger). Off by default. Use only for transport-level debugging; the dumps include payload bytes and can be verbose.

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {}
}
```

**Errors:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "error": {
    "code": -32603,
    "message": "could not open serial port COM7: Port already in use"
  }
}
```

---

### write

Sends data to the serial port.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "write",
  "params": {
    "message": "AQIDBA==",
    "encoding": "base64"
  }
}
```

**Parameters:**
- `message` (string, required) — Data to send (base64-encoded)
- `encoding` (string, required) — Always "base64"

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "sentBytes": 4
  }
}
```

**Side Effects:**
- Resets the keep-alive timer
- Last sent packet is cached for keep-alive resend

---

### startReading

Enables data reception (usually implicit after connect).

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "startReading",
  "params": {}
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {}
}
```

---

### stopReading

Disables data reception (keep-alive timer continues running).

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "stopReading",
  "params": {}
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": {}
}
```

---

### setKeepAlive

Toggle or reconfigure keep-alive at runtime, without disconnecting. Use to disable keep-alive before a firmware update and re-enable it afterwards, or to change the interval mid-session.

**Request — disable:**
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "setKeepAlive",
  "params": { "intervalMs": null }
}
```

**Request — enable / change interval:**
```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "method": "setKeepAlive",
  "params": { "intervalMs": 33 }
}
```

**Parameters:**
- `intervalMs` (int or null, required) — Interval in milliseconds. `null`, `0`, or negative values **disable** keep-alive. Positive values (re)start it with the given interval.

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": { "intervalMs": 33 }
}
```

The `result.intervalMs` echoes the **applied** interval (`null` when disabled). Use this to confirm the operation took effect.

**Side Effects:**
- If keep-alive was already running, the existing timer is stopped (blocking on any in-flight tick) before the new one starts. The call is fully idempotent.
- The cached last-TX packet is **preserved** across the toggle, so re-enabling keep-alive immediately resumes resending the same packet.

**Typical DFU sequence (client-side):**
```javascript
// 1. Disable keep-alive before bootloader entry
await link.send("setKeepAlive", { intervalMs: null });

// 2. Run firmware update (writes/reads as usual)
await runDfu(...);

// 3. Re-enable keep-alive for normal operation
await link.send("setKeepAlive", { intervalMs: 33 });
```

---

### disconnect

Closes the serial port connection.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "disconnect",
  "params": {}
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {}
}
```

**Side Effects:**
- Stops the keep-alive timer
- Closes the port
- Does NOT fire `serialDidDisconnect` notification (client-initiated close)

---

## Notifications

### didDiscoverPeripheral

Sent for each discovered serial port during discovery.

```json
{
  "jsonrpc": "2.0",
  "method": "didDiscoverPeripheral",
  "params": {
    "peripheralId": "port-0",
    "name": "COM7 (CH340)",
    "path": "COM7",
    "vendorId": "0x1a86",
    "productId": "0x7523",
    "rssi": 0
  }
}
```

---

### serialDidReceiveData

Sent when data is received on the serial port.

```json
{
  "jsonrpc": "2.0",
  "method": "serialDidReceiveData",
  "params": {
    "message": "SG93IGFyZSB5b3U/",
    "encoding": "base64"
  }
}
```

---

### serialDidDisconnect

Sent when the connection is lost (external cause, not client-initiated).

```json
{
  "jsonrpc": "2.0",
  "method": "serialDidDisconnect",
  "params": {
    "reason": "device",
    "message": "Port was removed"
  }
}
```

**Disconnect Reasons:**
- `"device"` — Device physically disconnected or a read-side `IOException` occurred (cable unplug, USB stack hiccup, driver error, transient USB noise that the kernel surfaced as an I/O error)
- `"error"` — Unexpected non-I/O exception in the read loop
- `"user"` — User action (rare)
- `"shutdown"` — Application shutting down

**Recovery policy (current):**

Scratch Link does **not** retry on I/O errors. The moment the kernel surfaces a read-side `IOException`, Link:
1. Fires `serialDidDisconnect` with `reason: "device"`.
2. Closes the port.
3. Stops the keep-alive timer and the RX loop.

The client (aluxlabs) is responsible for any reconnect logic — including any debounce or retry policy for transient USB noise.

This is a deliberate design choice for v1: keep Link's transport thin and predictable, let the client decide policy. If transient-disconnect reports start to accumulate from the field, we can re-negotiate a Link-side retry (e.g. one 200 ms re-open before surfacing the disconnect) and add a `retryOnIoError` connect parameter to opt in. Until then, **assume immediate disconnect on any I/O error.**

---

## Complete Example: Codetinker Connection

```javascript
// 1. Discover ports
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "discover",
  "params": {}
}
// → didDiscoverPeripheral: { peripheralId: "port-0", name: "COM7 (CH340)", ... }

// 2. Connect with keep-alive
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "connect",
  "params": {
    "peripheralId": "port-0",
    "baudRate": 115200,
    "peripheralType": "codetinker",
    "keepAliveIntervalMs": 33
  }
}
// → result: {}
// → Keep-alive timer starts, will resend last TX packet every 33ms if idle

// 3. Send command
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "write",
  "params": {
    "message": "AQIDBA==",
    "encoding": "base64"
  }
}
// → result: { sentBytes: 4 }
// → Keep-alive timer resets (cached packet = AQIDBA==)

// 4. Receive response
// ← serialDidReceiveData: { message: "BwgJCg==", encoding: "base64" }

// 5. Disconnect
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "disconnect",
  "params": {}
}
// → result: {}
// → Keep-alive timer stops
```

---

## Error Codes

| Code | Message | Description |
|------|---------|-------------|
| -32600 | Invalid Request | Malformed JSON-RPC |
| -32601 | Method not found | Unknown method |
| -32602 | Invalid params | Missing required parameter |
| -32603 | Internal error | Port error, invalid state, etc. |

---

## Recommendations

### For Codetinker
```json
{
  "baudRate": 115200,
  "peripheralType": "codetinker",
  "keepAliveIntervalMs": 33
}
```

### For Generic Serial Devices (no keep-alive)
```json
{
  "baudRate": 9600
}
```

### For Firmware Updates

Two layers of protection:

1. **Automatic (no client change needed).** Each `write` resets the keep-alive interval, so a burst of writes (DFU chunks) suppresses the resend until the line goes idle again.
2. **Explicit (recommended for wireless DFU).** Before bootloader entry, call `setKeepAlive` with `intervalMs: null` to disable keep-alive entirely. Re-enable after DFU completes. This eliminates any chance of a resend racing with a bootloader handshake on a slow wireless link.

```javascript
await link.send("setKeepAlive", { intervalMs: null });
// ... run DFU ...
await link.send("setKeepAlive", { intervalMs: 33 });
```

### For Transport-Level Debugging

Enable `wireTrace: true` on `connect` to get per-write/per-read hex dumps via `Trace.WriteLine`. Output is visible in [DebugView](https://learn.microsoft.com/sysinternals/downloads/debugview) (run as admin, "Capture Win32") or any attached debugger. Format:

```
wire-trace TX 12B 4c 4f 41 44 ...
wire-trace RX 31B 3c 1e af 00 ...
wire-trace TX(keep-alive) 4B aa bb cc dd
```

Buffers longer than 256 bytes are truncated with `…(+NB)` suffix. Compare these against the client's own per-message log to localize any drops or corruption.

---

**API Version**: 1.1  
**Last Updated**: 2026-05-25
