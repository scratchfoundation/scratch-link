# Bluetooth Peripheral Protocol

This document describes the communication protocol used by a Scratch Extension (or the extension framework) to
communicate with a Bluetooth RFCOMM / BR / EDR peripheral using Scratch Link. This document builds on the "Network
Protocol" document describing the portions of the protocol common to all peripheral types.

## Communication Interface (Scratch Extension to Scratch Link)

### Initiating Communication with Scratch Link

For Bluetooth (BT) connections, an extension connects to Scratch Link's WebSocket server at the path "/scratch/bt".

### Common Methods

Methods in this section must be supported by all session types. This section documents any protocol-specific qualities
of these methods.

#### Request: `getVersion`

*Added in network protocol version 1.2*

No additional version information is provided beyond the base implementation.

### Initial State

TODO: describe Bluetooth discovery / filtering parameters.

JSON-RPC **request** sent from Scratch Extension to Scratch Link to initiate discovery.

```json5
{
  "jsonrpc": "2.0",     // JSON-RPC version indicator
  "id": 1,              // Message sequence identifier
  "method": "discover", // Command identifier
  "params": {...}       // Transport Protocol-specific filtering information
}
```

JSON-RPC **response** sent from Scratch Link to Scratch Extension upon successful initiation of discovery. This confirms
the transition into the discovery state.

```json5
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 1,          // Message sequence identifier
  "result": null    // Presence of this property indicates success
}
```

JSON-RPC **response** sent from Scratch Link to Scratch Extension upon failure to initiate discovery. The connection
remains in the initial state.

```json5
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 1,          // Message sequence identifier
  "error": {...}    // Error information
}
```

### Connected State

#### Sending a Message

Sending data to a connected peripheral shall be initiated by the Scratch Extension. This command requires two arguments:
the message body and a supported encoding format. Attempting to "send" to a peripheral with an unsupported encoding or
invalid message body will result in an error response. If the underlying peripheral connection has specific needs
regarding packet size (MTU), keep-alive, etc., those concerns shall be managed by Scratch Link in order to simulate a
persistent free-form serial data stream.

JSON-RPC **request** sent from Scratch Extension to Scratch Link to send a serial message to a specified peripheral.

```json5
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

JSON-RPC **response** sent from Scratch Link to Scratch Extension upon successful message send.

```json5
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 5,          // Message sequence identifier
  "result": 4       // Number of bytes sent to peripheral
}
```

JSON-RPC **response** sent from Scratch Link to Scratch Extension upon unsuccessful message send.

```json5
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 5,          // Message sequence identifier
  "error": {...}    // Error information
}
```

### Receiving a Message

Receiving data from a connected peripheral shall be initiated by Scratch Link. This message requires two arguments: the
message body and the encoding format (`base64`). The Scratch Extension is not expected to return a "callback" response
when receiving a message.

JSON-RPC **notification** sent from Scratch Link to Scratch Extension on receipt of a serial message.

```json5
{
  "jsonrpc": "2.0",              // JSON-RPC version indicator
  "method": "didReceiveMessage", // Command identifier
  "params": {
    "message": "cG9uZw==",       // Message to be sent
    "encoding": "base64"         // Encoding of message to be sent
  }
}
```
