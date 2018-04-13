# Network Protocol

This document describes the proposed communication protocol used by the Scratch extension framework (the client) to
communicate with the device provider application (the server).

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

### Discover

Discovery for BT is fundamentally different than for BLE. Due to this difference we propose the following interface
that handles both scanning for and reporting discovered devices. A discovery scan requires two arguments: a vendor
identifier and a service identifier. Failure to provide both from the Scratch Extension shall result in the SDM
refusing to perform a scan. This is to help ensure the privacy and safety of the user as further outlined in the
"Implementation" section below.

JSON-RPC **request** sent from Scratch Extension to SDM to initiate discovery.
```json
{
  "jsonrpc": "2.0",     // JSON-RPC version indicator
  "id": 1,              // Message sequence identifier
  "method": "discover", // Command identifier
  "params": {
    "vendor": 0x0397,   // Vendor identifier filter
    "service": 0x0000   // Service identifier filter
  }
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon successful initiation of discovery.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 1,          // Message sequence identifier
  "result": null    // Presence of this property indicates success
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon failure to initiate discovery.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 1,          // Message sequence identifier
  "error": {...}    // Error information
}
```

JSON-RPC **notification** sent from SDM to Scratch Extension upon discovery of peripherals. Note that this message may
be passed from the SDM to the Scratch Extension many times for as long as the discovery process is initiated.
```json
{
  "jsonrpc": "2.0",                  // JSON-RPC version indicator
  "method": "didDiscoverPeripheral", // Command identifier
  "params": {
    "uuid": 0x000,                   // Unique identifier for peripheral
    "name": "EV3",                   // Name
    "rssi": -70                      // Signal strength indicator
  }
}
```

### Connect

Similar to the BLE interface, connection shall be initiated by the Scratch Extension by providing a specified unique
identifier (UUID) for the BT peripheral with which to connect. Confirmation and error handling is communicated using a
"callback" pattern that is documented with more detail in the "Implementation" section below. Attempting to connect to
a peripheral which does not match the vendor and service identifiers provided in a prior connection phase will result
in an error response.

JSON-RPC **request** sent from Scratch Extension to SDM to connect to a peripheral.
```json
{
  "jsonrpc": "2.0",    // JSON-RPC version indicator
  "id": 3,             // Message sequence identifier
  "method": "connect", // Command identifier
  "params": {
    "uuid": 0x0000     // Unique identifier for peripheral
  }
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon successful connection.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 3,          // Message sequence identifier
  "result": null    // Indicates success
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon connection failure.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 3,          // Message sequence identifier
  "error": {...}    // Error information
}
```

### Disconnect

Disconnection is also handled similarly to the BLE interface, with a single "disconnect" event that may be dispatched
by the Scratch Extension resulting in one of two "callback" responses. Attempting to "disconnect" from a peripheral
which is not currently connected will result in an error response.

JSON-RPC **request** sent from Scratch Extension to SDM to disconnect from a peripheral.
```json
{
  "jsonrpc": "2.0",       // JSON-RPC version indicator
  "id": 4,                // Message sequence identifier
  "method": "disconnect", // Command identifier
  "params": {
    "uuid": 0x0000        // Unique identifier for peripheral
  }
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon successful disconnection.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 4,          // Message sequence identifier
  "result": null    // Indicates success
}
```

JSON-RPC **response** sent from SDM to Scratch Extension upon disconnection failure.
```json
{
  "jsonrpc": "2.0", // JSON-RPC version indicator
  "id": 4,          // Message sequence identifier
  "error": {...}    // Error information
}
```

### Sending a Message

Sending serial messages over the Bluetooth Classic interface shall be initiated by the Scratch Extension. This command
requires three arguments including the unique identifier (UUID) of the peripheral with which to communicate, the
message body, and a supported encoding format. Attempting to "send" to a peripheral which is not currently connected
will result in an error response. Attempting to "send" to a peripheral with an unsupported encoding or invalid message
body will also result in an error response.

JSON-RPC **request** sent from Scratch Extension to SDM to send a serial message to a specified peripheral.
```json
{
  "jsonrpc": "2.0",        // JSON-RPC version indicator
  "id": 5,                 // Message sequence identifier
  "method": "send",        // Command identifier
  "params": {
    "uuid": 0x0000,        // Unique identifier for peripheral
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

Receiving serial messages over the Bluetooth Classic interface shall be initiated by the Scratch Device Manager. This
command requires three arguments including the unique identifier (UUID) of the peripheral which has initiated the
message, the message body, and the default encoding format (UTF-8). The Scratch Extension is not expected to return a
"callback" response when receiving a message.

JSON-RPC **notification** sent from SDM to Scratch Extension on receipt of a serial message.
```json
{
  "jsonrpc": "2.0",              // JSON-RPC version indicator
  "method": "didReceiveMessage", // Command identifier
  "params": {
    "uuid": 0x0000,              // Unique identifier for peripheral
    "message": "cG9uZw==",       // Message to be sent
    "encoding": "base64"         // Encoding of message to be sent
  }
}
```
