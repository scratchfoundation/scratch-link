# Bluetooth LE Peripheral Protocol

This document describes the communication protocol used by a Scratch Extension (or the extension framework) to
communicate with a Bluetooth Low Energy (BLE) peripheral's GATT interface using Scratch Link. This document builds on
the "Network Protocol" document describing the portions of the protocol common to all peripheral types.

## Communication Interface (Scratch Extension to Scratch Link)

In general, BLE support in Scratch Link is patterned after BLE support in Web bluetooth. The Web Bluetooth specification
can be found here: <https://webbluetoothcg.github.io/web-bluetooth/>

### Initiating Communication with Scratch Link

For BLE connections, an extension connects to Scratch Link's WebSocket server at the path "/scratch/ble".

### Common Methods

Methods in this section must be supported by all session types. This section documents any protocol-specific qualities
of these methods.

#### Request: `getVersion`

*Added in network protocol version 1.2*

No additional version information is provided beyond the base implementation.

### Initial State

The Scratch Extension may initiate discovery by sending a "discover" request. The parameters of the "discover" request
shall include an array-valued "filters" property and may also contain an array-valued "optionalServices" property.

The "filters" array shall contain at least one item, and each item must represent a non-trivial filter. A peripheral
is accepted by a filter if the peripheral matches **every** condition in the filter; a peripheral matches a "discover"
request if the peripheral matches **any** filter in the "filters" array.

A filter object shall contain one or more of the following properties:

- "name": to pass this condition, the peripheral's advertised name must match this string exactly.
- "namePrefix": to pass this condition, the peripheral's advertised name must begin with this string.
- "services": to pass this condition, every service named in this array-valued property must be advertised by the
  peripheral. See the "Service Names" section below for more information on specifying services in this list.
- "manufacturerData": to pass this condition, the peripheral must advertise manufacturer data for each manufacturer ID
  key in this key-value object. If the value associated with a particular manufacturer ID is an object with a
  "dataPrefix" property, a "mask" property, or both, then the data advertised by the peripheral must match as
  as described by the Web Bluetooth specification for [matching "BluetoothDataFilterInit"](
  https://webbluetoothcg.github.io/web-bluetooth/#bluetoothdatafilterinit-matches).
  - If "dataPrefix" is present it must be an array of integers. If absent it is treated as an empty arry.
  - If "mask" is present it must be an array of integers. If absent it is treated as an array of 255 (0xFF) with
    length equal to the length of the "dataPrefix" array.

The "optionalServices" array, if present, shall contain service names which the Scratch Extension would like to access
even if they are not used for filtering. See the "Service Names" section below for more information on specifying
services in this list, or the "Connected State" section for more information about the relationship between the
"services" filter property, the "optionalServices" parameter, and the services available to the Scratch Extension.

Example JSON-RPC **request** sent from Scratch Extension to Scratch Link to initiate discovery:

```json5
{
  "jsonrpc": "2.0",     // JSON-RPC version indicator
  "id": 1,              // Message sequence identifier
  "method": "discover", // Command identifier
  "params": {
    "filters": [
      { "name": "My Peripheral" },               // Accept peripheral named exactly "My Peripheral"
      { "services": [ 0x1815, "current_time" ]}, // Accept peripheral with both "Automation IO" and "Current Time" services
      {
        // Accept peripheral advertising data under manufacturer ID 17
        // where the low nybble of the first byte of that data is 0x1 (0x01, 0x11, 0x81, etc.)
        // and the second byte of that data is exactly 0x2A.
        "manufacturerData": {
          "17": {
            "dataPrefix": [0x01, 0x2A]
            "mask": [0x0F, 0xFF]
          }
        }
      }
    ],
    "optionalServices": [
      "00001826-0000-1000-8000-00805f9b34fb"  // Allow the "Fitness Machine" service if present
    ]
  }
}
```

#### Comparison to Web Bluetooth

Discovery of a BLE peripheral mimics the
[Web Bluetooth specification](https://webbluetoothcg.github.io/web-bluetooth/#device-discovery) with a few exceptions:

- The "acceptAllDevices" property is not allowed.
- The "filters" list must contain at least one filter.
- Each filter in the "filters" list must be non-trivial. For example, a filter which contains only an empty
  "namePrefix" is not allowed.
- The "serviceData" filter property is not supported.

#### Service Names

The "services" array and, if present, the "optionalServices" array shall contain what the Web Bluetooth specification
calls "names" for GATT services. A GATT service name is one of the following:

- A full GATT service UUID in string format, such as "0000180f-0000-1000-8000-00805f9b34fb"
  - Hexadecimal characters shall be lower-case
- A "short ID" in integer format, such as 0x180f
- A name from the [Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services) without the
  "org.bluetooth.service." prefix, such as "battery_service"

Each of the examples above specifies the same service.

Scratch Link shall resolve each name to a full UUID using the [getService](
https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothuuid-getservice) algorithm as described by the Web
Bluetooth specification, which in practice is just shorthand for calling the [resolveUuidName](
https://webbluetoothcg.github.io/web-bluetooth/#resolveuuidname) algorithm and passing the [Service Assigned Numbers
table](https://www.bluetooth.com/specifications/gatt/services) and "org.bluetooth.service" for the prefix.

### Connected State

Connecting to a BLE peripheral with a "connect" request is the Scratch Link equivalent of the [Web Bluetooth
`device.gatt.connect()`](https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothremotegattserver-connect) call.
After successfully connecting to a peripheral the Scratch Extension may access any **allowed** GATT service which the
peripheral provides by reading and writing characteristics of those services, etc.

Scratch Link shall block access to certain GATT UUIDs (services, characteristics, etc.) as demanded by the [GATT
Blocklist](https://webbluetoothcg.github.io/web-bluetooth/#the-gatt-blocklist). Such UUIDs are allowed in discovery
filters but not allowed for actual communication.

#### Allowed Services

Scratch Link shall reject any attempt by the Scratch Extension to access a GATT service unless the service is
specifically allowed. A service is allowed if and only if:

- it was named in the "services" array of **any** filter in the "discover" request, **or**
- it was named in the "optionalServices" array of the "discover" request.

Consider this request:

```json5
{
  "jsonrpc": "2.0",     // JSON-RPC version indicator
  "id": 1,              // Message sequence identifier
  "method": "discover", // Command identifier
  "params": {
    "filters": [
      { "name": "My Peripheral" },              // Accept peripheral named exactly "My Peripheral"
      { "services": [ 0x1815, "current_time" ]} // Accept peripheral with both "Automation IO" and "Current Time" services
    ],
    "optionalServices": [
      "00001826-0000-1000-8000-00805f9b34fb"  // Allow the "Fitness Machine" service if present
    ]
  }
}
```

Suppose Scratch Link finds a peripheral with the name "My Peripheral" and reports that to the client in a
"didDiscoverPeripheral" notification, then the Scratch Extension chooses to connect to the "My Peripheral" peripheral.
The Scratch Extension will be allowed to contact the following services:

- Service 0x1805, the "Current Time" service, with UUID 00001805-0000-1000-8000-00805f9b34fb
- Service 0x1815, the "Automation IO" service, with UUID 00001815-0000-1000-8000-00805f9b34fb
- Service 0x1826, the "Fitness Machine" service, with UUID 00001826-0000-1000-8000-00805f9b34fb

Note that the peripheral may or may not implement each of these services: in fact, since the peripheral satisfied the
discovery filter based on its name it might not implement any of these services.

#### Enumerating Services

The Scratch Extension may query the list of allowed services by sending a "getServices" **request** to Scratch Link:

```json5
{
  "jsonrpc": "2.0",        // JSON-RPC version indicator
  "id": 3,                 // Message sequence identifier
  "method": "getServices", // Command identifier
  "params": {}             // No parameters
}
```

On success, Scratch Link's **response** shall contain an array of service UUIDs as its result:

```json5
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    "0000180f-0000-1000-8000-00805f9b34fb"
  ]
}
```

The Scratch Extension is not required to enumerate a peripheral's services; Scratch Link shall not change the list of
allowed services based on whether or not the Scratch Extension has requested enumeration.

#### Enumerating Service Characteristics (not currently implemented)

The Scratch Extension may query the list of characteristics available on an allowed service by sending a
"getCharacteristics" **request** to Scratch Link:

```json5
{
  "jsonrpc": "2.0",                // JSON-RPC version indicator
  "id": 4,                         // Message sequence identifier
  "method": "getCharacteristics",  // Command identifier
  "params": {
    "serviceId": "battery_service" // GATT service to query
  }
}
```

The "serviceId" property may be any valid GATT service name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services).

On success, Scratch Link's **response** shall contain an array of characteristic UUIDs as its result:

```json5
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": [
    "00002a19-0000-1000-8000-00805f9b34fb"
  ]
}
```

The Scratch Extension is not required to enumerate a service's characteristics; Scratch Link shall not change the list
of allowed characteristics based on whether or not the Scratch Extension has requested enumeration.

#### Writing to a Characteristic

The Scratch Extension may write data to a characteristics available on an allowed service by sending a "write"
**request** to Scratch Link:

```json5
{
  "jsonrpc": "2.0",                            // JSON-RPC version indicator
  "id": 5,                                     // Message sequence identifier
  "method": "write",                           // Command identifier
  "params": {
    "serviceId": "battery_service",            // Optional: GATT service to write
    "characteristicId": "battery_level_state", // GATT characteristic to write
    "message": "cGluZw==",                     // Content to be written
    "encoding": "base64",                      // Optional: encoding used by the "message" property
    "withResponse": true                       // Optional: whether or not to wait for the peripheral's response
  }
}
```

The "serviceId" property may be any valid GATT service name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services).

The "serviceId" property may be omitted; in this case the peripheral's primary service will be assumed. The primary
service shall be determined the same way as in [the Web Bluetooth `getPrimaryService(service)` call](
https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothremotegattserver-getprimaryservice).

The "characteristicId" property may be any valid GATT characteristic name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Characteristic Assigned Numbers table](
  https://www.bluetooth.com/specifications/gatt/characteristic).

The "encoding" property may be omitted; in this case the "message" is assumed to be a Unicode string. Scratch Link shall
encode the string using UTF-8 and write the resulting bytes to the characteristic.

Bluetooth LE supports writing a value to a characteristic with or without a response. The "withResponse" property
controls which of these modes shall be used for a particular write.

- If true, Scratch Link shall write with response. That is, Scratch Link shall wait for the peripheral to confirm that
  the write was received without error, and Scratch Link's response to the client shall report any error reported by the
  BLE peripheral. If the peripheral reports an error, that error shall be forwarded to the client as an error response
  to the "write" request.
- If false, Scratch Link shall write without response. That is, Scratch Link shall make a [best-effort
  delivery](https://en.wikipedia.org/wiki/Best-effort_delivery) attempt then report success. There is no way for the
  peripheral to report an error in this mode.
- If absent, Scratch Link shall check if the characteristic appears to support writing without response. If so,
  Scratch Link shall write without response. Otherwise, Scratch shall write with response.

Generally, writing without response is significantly faster.

On success, Scratch Link's **response** shall contain the number of bytes written, which may differ from the number of
characters in the string value of the initiating request's "message" property:

```json5
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": 4
}
```

#### Reading from a Characteristic

The Scratch Extension may read data from a characteristics available on an allowed service by sending a "read"
**request** to Scratch Link:

```json5
{
  "jsonrpc": "2.0",                            // JSON-RPC version indicator
  "id": 6,                                     // Message sequence identifier
  "method": "read",                            // Command identifier
  "params": {
    "serviceId": "battery_service",            // Optional: GATT service to read
    "characteristicId": "battery_level_state", // GATT characteristic to read
    "encoding": "base64",                      // Optional: Encoding requested to be used in the response
    "startNotifications": true                 // Optional: Whether or not to register for value change notifications
  }
}
```

The "serviceId" property may be any valid GATT service name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services).

The "serviceId" property may be omitted; in this case the peripheral's primary service will be assumed. The primary
service shall be determined the same way as in [the Web Bluetooth `getPrimaryService(service)` call](
https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothremotegattserver-getprimaryservice).

The "characteristicId" property may be any valid GATT characteristic name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Characteristic Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/characteristic).

If the "encoding" property is present then Scratch Link should use the indicated encoding for the response, but Scratch
Link is not required to do so. If the "encoding" property is absent in the **request** Scratch Link may choose an
encoding for the response.

On success, Scratch Link's **response** shall contain the data read from the characteristic:

```json5
{
  "jsonrpc": "2.0",        // JSON-RPC version indicator
  "id": 6,                 // Message sequence identifier
  "result": {
    "message": "cG9uZw==", // The data read from the characteristic
    "encoding": "base64"   // Optional: Encoding used by the "message" property
  }
}
```

If the "encoding" property is absent in the **response**, the Scratch Extension should assume that the "message"
property contains a Unicode string.

If the "startNotifications" property is both present and true in the **request**, this is equivalent to also sending a
"startNotifications" request with the same parameters (see next section).

#### Value change notification

The Scratch Extension may request that Scratch Link shall continuously notify the Scratch Extension of changes in the
characteristic's value by sending a "startNotifications" **request**. This shall continue until the Scratch Extension
makes a "stopNotifications" request:

```json5
{
  "jsonrpc": "2.0",                            // JSON-RPC version indicator
  "id": 7,                                     // Message sequence identifier
  "method": "startNotifications",              // Command identifier
  "params": {
    "serviceId": "battery_service",            // Optional: GATT service to read
    "characteristicId": "battery_level_state", // GATT characteristic to read
    "encoding": "base64"                       // Optional: Encoding requested to be used in the response
  }
}
```

The "serviceId" property may be any valid GATT service name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services).

The "serviceId" property may be omitted; in this case the peripheral's primary service shall be assumed. The primary
service shall be determined the same way as in [the Web Bluetooth `getPrimaryService(service)` call](
https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothremotegattserver-getprimaryservice).

The "characteristicId" property may be any valid GATT characteristic name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Characteristic Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/characteristic).

If the "encoding" property is present then Scratch Link should use the indicated encoding for notifications, but Scratch
Link is not required to do so. If the "encoding" property is absent in the **request** Scratch Link may choose an
encoding for the response.

Scratch Link notifies the Scratch Extension of value changes with **notification** messages in this form:

```json5
{
  "jsonrpc": "2.0",                    // JSON-RPC version indicator
  "method": "characteristicDidChange", // Command identifier
  "params": {
    "serviceId": "0000180f-0000-1000-8000-00805f9b34fb" // UUID of the service which hosts the changed characteristic
    "characteristicId": "00002a19-0000-1000-8000-00805f9b34fb", // UUID of the characteristic whose value changed
    "message": "cG9uZw==",             // The data read from the characteristic
    "encoding": "base64"               // Optional: Encoding used by the "message" property
  }
}
```

If the "encoding" property is absent the Scratch Extension should assume that the "message" property contains a
Unicode string.

Scratch Link shall only send such a notification when the value of a characteristic changes, and only for
characteristics for which a "startNotifications" request (or a "read" request with the "startNotifications" flag set)
has been made. Such notifications shall continue until the Scratch Extension makes a "stopNotifications" request.

#### Stop Notifications

The Scratch Extension may end value change notifications by sending a "stopNotifications" **request** to Scratch Link:

```json5
{
  "jsonrpc": "2.0",                           // JSON-RPC version indicator
  "id": 8,                                    // Message sequence identifier
  "method": "stopNotifications",              // Command identifier
  "params": {
    "serviceId": "battery_service",           // Optional: GATT service for which to stop notifications
    "characteristicId": "battery_level_state" // GATT characteristic for which to stop notifications
  }
}
```

The "serviceId" property may be any valid GATT service name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services).

The "serviceId" property may be omitted; in this case the peripheral's primary service will be assumed. The primary
service shall be determined the same way as in [the Web Bluetooth `getPrimaryService(service)` call](
https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothremotegattserver-getprimaryservice).

The "characteristicId" property may be any valid GATT characteristic name:

- a string representing a full UUID,
- an integer representing a short ID, or
- a string name from the [Characteristic Assigned Numbers table](
  https://www.bluetooth.com/specifications/gatt/characteristic).
