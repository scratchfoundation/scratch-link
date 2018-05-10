# Bluetooth Device Protocol

This document describes the proposed communication protocol used by a Scratch Extension (or the extension framework) to
communicate with a Bluetooth RFCOMM / BR / EDR peripheral using the Scratch Device Manager (SDM). This document builds
on the "Network Protocol" document describing the portions of the protocol common to all peripheral types.

## Proposed Interface (Scratch Extension to Scratch Device Manager)

### Initiating Communication with SDM

For Bluetooth (BT) connections, an extension connects to the SDM’s WebSocket server at the path "/scratch/bt".

### Initial State

TODO: describe Bluetooth discovery / filtering parameters.

JSON-RPC **request** sent from Scratch Extension to SDM to initiate discovery.
```json
{
  "jsonrpc": "2.0",     // JSON-RPC version indicator
  "id": 1,              // Message sequence identifier
  "method": "discover", // Command identifier
  "params": {...}       // Transport Protocol-specific filtering information
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon successful initiation of discovery. This confirms the
transition into the discovery state.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 1,          // Message sequence identifier
  "result": null    // Presence of this property indicates success
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon failure to initiate discovery. The connection remains in
the initial state.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 1,          // Message sequence identifier
  "error": {...}    // Error information
}
```

### Connected State

#### Sending a Message

Sending data to a connected peripheral shall be initiated by the Scratch Extension. This command requires two
arguments: the message body and a supported encoding format. Attempting to "send" to a peripheral with an unsupported
encoding or invalid message body will result in an error response. If the underlying peripheral connection has specific
needs regarding packet size (MTU), keep-alive, etc., those concerns shall be managed by the SDM in order to simulate a
persistent free-form serial data stream.

JSON-RPC **request** sent from Scratch Extension to SDM to send a serial message to a specified peripheral.
```json
{
  "jsonrpc": "2.0",        // JSON-RPC version indicator
  "id": 5,                 // Message sequence identifier
  "method": "send",        // Command identifier
  "params": {
    "message": "cGluZw==", // Message to be sent
    "encoding": "base64"   // Encoding of message to be sent
  }
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon successful message send.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 5,          // Message sequence identifier
  "result": 4       // Number of bytes sent to peripheral
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon unsuccessful message send.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 5,          // Message sequence identifier
  "error": {...}    // Error information
}
```

### Receiving a Message

Receiving data from a connected peripheral shall be initiated by the Scratch Device Manager. This message requires
two arguments: the message body and the encoding format (`base64`). The Scratch Extension is not expected to return a
"callback" response when receiving a message.

JSON-RPC **notification** sent from SDM to Scratch Extension on receipt of a serial message.
```json
{
  "jsonrpc": "2.0",              // JSON-RPC version indicator
  "method": "didReceiveMessage", // Command identifier
  "params": {
    "message": "cG9uZw==",       // Message to be sent
    "encoding": "base64"         // Encoding of message to be sent
  }
}
```
