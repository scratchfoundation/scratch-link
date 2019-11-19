# Network Protocol

This document describes the communication protocol used by a Scratch Extension (or the extension framework) to
communicate with Scratch Link. Scratch Link supports multiple types of peripheral; this document describes the portions
of the protocol which are common across peripheral types.

## Protocol Versioning

This protocol's version number applies to the protocol as a whole, including:

- The common portions of the protocol (this document) and
- The portions of the protocol which are specific to individual peripheral types, such as Bluetooth LE.

This protocol's version number does NOT apply to any particular implementation of the protocol.

This version number shall follow the Semantic Versioning specification, found here: <https://semver.org/>

### Version History

- Version 1.3:
  - Bluetooth LE:
    - Alter Scratch Link's handling of the `withResponse` flag on a `write` request. The flag now overrides Scratch
      Link's detection of GATT characteristic flags.
- Version 1.2:
  - Add `manufacturerData` filtering for BLE discovery.
  - Add common `getVersion` method.
- Version 1.1:
  - Add protocol version number.
  - Bluetooth LE:
    - Add `withResponse` flag to `write` request.
    - Add `startNotifications` request.
- Version 1.0:
  - Initial version.

## JSON-RPC 2.0

JSON-RPC is a specification for making remote procedure calls (RPC) using JavaScript Object Notation (JSON). The
specification is relatively lightweight and flexible, and in particular JSON-RPC messages are easy to build, parse, and
transfer with most programming languages and transport channels. This project uses version 2.0 of the JSON-RPC
specification, which describes three types of message: request, notification, and response.

The JSON-RPC 2.0 specification may be found here: <http://www.jsonrpc.org/specification>

## Communication Interface (Scratch Extension to Scratch Link)

### Initiating Communication with Scratch Link

Communication with Scratch Link is performed over WebSockets. When initiating a WebSocket connection between the Scratch
Extension and Scratch Link, the choice of path determines which Transport Protocol will be used. For example, when
initiating a BLE connection the extension connects to Scratch Link's WebSocket server at path `/scratch/ble`, and for
Bluetooth Classic (BT) connections the extension connects to the path `/scratch/bt`.

### Common Methods

Methods in this section must be supported by all session types. In general, these methods are stateless unless
otherwise specified.

#### Request: `getVersion`

*Added in network protocol version 1.2*

This is a JSON-RPC **request** sent from Scratch Extension to Scratch Link to retrieve version information about
Scratch Link itself. No parameters are necessary.

```json5
{
  "jsonrpc": "2.0",      // JSON-RPC version indicator
  "id": 1,               // Message sequence identifier
  "method": "getVersion" // Command identifier
}
```

JSON-RPC **response** sent from Scratch Link to Scratch Extension .

```json5
{
  "jsonrpc": "2.0",   // JSON-RPC version indicator
  "id": 1,            // Message sequence identifier
  "result": {
    "protocol": "1.2" // Version number for the overall network protocol
  }
}
```

The version number in the `protocol` property corresponds to the network protocol version defined in this document.
Other properties may be present and may contain more version information about the particular session; see
protocol-specific documentation for details.

### Stateful Connections

In contrast to previously proposed protocols, this protocol dedicates a particular connection to the discovery of and
interaction with exactly one peripheral. If an Extension wishes to interact with more than one peripheral simultaneously
then that Extension must open more than one connection to Scratch Link.

To this end, a particular socket connection may transition through several distinct states, each of which is described
below. Each state supports a particular set of requests and notifications, and sending a request or notification not
supported by the current state shall result in an error response and otherwise be ignored. For example, attempting to
read or write data from or to a peripheral while the connection is in the "discovery" state is an error.

### Initial State

The connection begins in an initial, dormant state. The only message supported in this state is a discovery request,
which will transition the connection into the discovery state. Scratch Link may terminate a connection which does not
successfully enter the discovery state within a reasonable amount of time.

A discovery request may include filtering information specific to the Transport Protocol associated with the connection.
For example, a BLE discovery request might include the UUIDs of one or more required GATT services or characteristics.

Note: discovery requests for wireless peripherals **must** include at least one non-trivial piece of filtering
information. Failure to provide such from the Scratch Extension shall result in Scratch Link refusing to perform a scan.
This is to help ensure the privacy and safety of the user.

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

### Discovery State

The discovery state lasts until the Scratch Extension requests to connect to a peripheral or disconnects. Scratch Link
shall manage the initiation and/or renewal of scan, enumeration, or other peripheral discovery requests with the host
system on an ongoing basis until the end of the discovery phase.

If an unreasonable amount of time passes without the Scratch Extension issuing a successful "connect" request or
disconnecting from the socket, Scratch Link may end discovery and close the socket connection. This may help save
battery power on mobile devices, for example.

This state supports the "didDiscoverPeripheral" notification (sent from Scratch Link to Scratch Extension) and the
"connect" request (sent from Scratch Extension to Scratch Link).

JSON-RPC **notification** sent from Scratch Link to Scratch Extension upon discovery of peripherals. Note that this
message may be passed from Scratch Link to the Scratch Extension many times for as long as the discovery state is
active.

```json5
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

The RSSI property indicates a relative signal strength for wireless peripherals, or a special value as described in this
table:

Value | Meaning
-|-
127 | The signal strength for this wireless peripheral is not (yet) known but may become valid later.
0 | The signal strength for this wireless peripheral is unknown and is not expected to become known later.
&lt; 0 | Numeric indicator of relative signal strength (-1 is stronger than -99) as an integer in unspecified units.
`null` | Signal strength does not make sense for this peripheral. For example, the peripheral may not be wireless.

Connection shall be initiated by the Scratch Extension by providing a specified peripheral identifier with which to
connect. Attempting to connect to a peripheral which does not match the filtering information provided in the discovery
request shall result in an error response.

JSON-RPC **request** sent from Scratch Extension to Scratch Link to connect to a peripheral.

```json5
{
  "jsonrpc": "2.0",        // JSON-RPC version indicator
  "id": 3,                 // Message sequence identifier
  "method": "connect",     // Command identifier
  "params": {
    "peripheralId": 0x0000 // Unique identifier for peripheral
  }
}
```

JSON-RPC **response** sent from Scratch Link to Scratch Extension upon successful connection. This confirms the
transition into the connected state.

```json5
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 3,          // Message sequence identifier
  "result": null    // Indicates success
}
```

JSON-RPC **response** sent from Scratch Link to Scratch Extension upon connection failure. The discovery state shall
remain active.

```json5
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

Web Sockets support both "text" and "binary" frames; either is acceptable for sending a JSON-RPC message between Scratch
Link and the Scratch Extension. It is acceptable to send one message as a text frame and the next as a binary frame. A
request and its response should match: for example, if a request is sent in a text frame the corresponding response
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

```json5
{
  "message": "cGluZw==", // Message content
  "encoding": "base64"   // Encoding used by the "message" property
}
```

JSON data in the above format should be interpreted as if it were a buffer of bytes: the "message" should be decoded
to bytes using the encoding method specified by the "encoding" property.

For example, a peripheral interface may implement a "send" **request** like this:

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

If the "encoding" property is omitted, the "message" is assumed to be a Unicode string.
