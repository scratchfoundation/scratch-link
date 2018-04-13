# Network Protocol

This document describes the proposed communication protocol used by a Scratch Extension (or the extension framework) to
communicate with the Scratch Device Manager (SDM).

## JSON-RPC 2.0

JSON-RPC is a specification for making remote procedure calls (RPC) using JavaScript Object Notation (JSON). The
specification is relatively lightweight and flexible, and in particular JSON-RPC messages are easy to build, parse, and
transfer with most programming languages and transport channels. This project uses version 2.0 of the JSON-RPC
specification, which describes three types of message: request, notification, and response.

The JSON-RPC 2.0 specification may be found here: http://www.jsonrpc.org/specification

## Proposed Interface (Scratch Extension to Scratch Device Manager)

### Initiating Communication with SDM

Communication with the SDM is performed over WebSockets. When initiating a WebSocket connection between the Scratch
Extension and the SDM, the choice of path determines which Transport Protocol will be used. For example, when
initiating a BLE connection, the extension connects to the SDM’s WebSocket server at path "/scratch/ble". For Bluetooth
Classic (BT) connections, we propose using the following connection namespace: "/scratch/bt".

### Stateful Connections

In contrast to previously proposed protocols, this proposal dedicates a particular connection to the discovery of and
interaction with exactly one peripheral. If an Extension wishes to interact with more than one peripheral
simultaneously then that Extension must open more than one connection to the SDM.

To this end, a particular socket connection may transition through several distinct states, each of which is described
below. Each state supports a particular set of requests and notifications, and sending a request or notification not
supported by the current state shall result in an error response and otherwise be ignored. For example, attempting to
send data to a peripheral using a "send" request while the connection is in the "discovery" state is a non-fatal error.

### Initial State

The connection begins in an initial, dormant state. The only message supported in this state is a discovery request,
which will transition the connection into the discovery state. The SDM may terminate a connection which does not
successfully enter the discovery state within a reasonable amount of time.

A discovery request may include filtering information specific to the Transport Protocol associated with the connection.
For example, a BLE discovery request might include the UUIDs of one or more required GATT services or characteristics.

Note: discovery requests for wireless peripherals **must** include at least one non-trivial piece of filtering
information. Failure to provide such from the Scratch Extension shall result in the SDM refusing to perform a scan.
This is to help ensure the privacy and safety of the user.

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

### Discovery State

The discovery state lasts until the Scratch Extension requests to connect to a peripheral or the discovery times out.
This state supports the "didDiscoverPeripheral" notification (sent from SDM to Scratch Extension) and the "connect"
request (sent from Scratch Extension to SDM).

JSON-RPC **notification** sent from SDM to Scratch Extension upon discovery of peripherals. Note that this message may
be passed from the SDM to the Scratch Extension many times for as long as the discovery state is active.
```json
{
  "jsonrpc": "2.0",                  // JSON-RPC version indicator
  "method": "didDiscoverPeripheral", // Command identifier
  "params": {
    "peripheralId": 0x0000,          // Unique identifier for peripheral
    "name": "EV3",                   // Name
    "rssi": -70                      // Signal strength indicator
  }
}
```

Connection shall be initiated by the Scratch Extension by providing a specified peripheral identifier with which to
connect. Attempting to connect to a peripheral which does not match the filtering information provided in a prior
connection phase shall result in an error response.

JSON-RPC **request** sent from Scratch Extension to SDM to connect to a peripheral.
```json
{
  "jsonrpc": "2.0",        // JSON-RPC version indicator
  "id": 3,                 // Message sequence identifier
  "method": "connect",     // Command identifier
  "params": {
    "peripheralId": 0x0000 // Unique identifier for peripheral
  }
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon successful connection. This confirms the transition into
the connected state.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 3,          // Message sequence identifier
  "result": null    // Indicates success
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon connection failure. The discovery state shall remain
active.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 3,          // Message sequence identifier
  "error": {...}    // Error information
}
```

### Connected State

The connected state indicates that the socket has become connected to a specific peripheral and further messages will
be dedicated to that peripheral. In this state, the Scratch Extension may send the "send" request and the SDM may send
the "didReceiveMessage" notification. To disconnect from the current peripheral or discover a new peripheral the
Scratch Extension must disconnect this socket and connect to a new one.

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
