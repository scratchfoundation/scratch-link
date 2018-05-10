# Network Protocol

This document describes the proposed communication protocol used by a Scratch Extension (or the extension framework) to
communicate with the Scratch Device Manager (SDM). The SDM supports multiple types of peripheral; this document
describes the portions of the protocol which are common across peripheral types.

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
read or write data from or to a peripheral while the connection is in the "discovery" state is an error.

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

The discovery state lasts until the Scratch Extension requests to connect to a peripheral or disconnects. The SDM shall
manage the initiation and/or renewal of scan, enumeration, or other peripheral discovery requests with the host system
on an ongoing basis until the end of the discovery phase.

If an unreasonable amount of time passes without the Scratch Extension issuing a successful "connect" request or
disconnecting from the socket, the SDM may end discovery and close the socket connection. This may help save battery
power on mobile devices, for example.

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
connect. Attempting to connect to a peripheral which does not match the filtering information provided in the discovery
request shall result in an error response.

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
be dedicated to that peripheral. To disconnect from the current peripheral or discover a new peripheral the Scratch
Extension must disconnect this socket and connect to a new one.

The details of peripheral communication depend on the type of peripheral and are documented separately. This section
describes a few conventions which should be used in peripheral communication protocols when reasonable.

#### Message encoding

Web Sockets support both "text" and "binary" frames; either is acceptable for sending a JSON-RPC message between the
SDM and the Scratch Extension. It is acceptable to send one message as a text frame and the next as a binary frame.
A request and its response should match: for example, if a request is sent in a text frame the corresponding response
should be sent in a text frame.

When a JSON-RPC message is sent in a text frame, the JSON object shall occupy the whole message. The Web Socket
specification covers how text frames are encoded went sent over a Web Socket and how text frames are decoded when
received over a web socket; no additional text encoding is necessary or allowed.

When a JSON-RPC message is sent in a binary frame, the text shall be encoded to bytes using UTF-8 encoding. The
resulting buffer of UTF-8 bytes shall occupy the whole message.

Peripheral protocols should use only printable ASCII characters in method, parameter, and property names. Ideally, all
method, parameter, and property names should be legal identifiers in JavaScript, C#, and Swift.

#### Data Buffers

Using JSON-RPC implies that all message payloads must be text, but communication with peripherals may require sending
or receiving binary data. Binary data must therefore be encoded into a JSON-friendly string. In general, a message
which contains a buffer of data should take the following format:
```json
{
  "message": "cGluZw==", // Message content
  "encoding": "base64"   // Encoding used by the "message" property
}
```

JSON data in the above format should be interpreted as if it were a buffer of bytes: the "message" should be decoded
to bytes using the encoding method specified by the "encoding" property.

For example, a peripheral interface may implement a "send" **request** like this:
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

If the "encoding" property is omitted, the "message" is assumed to be a Unicode string.
