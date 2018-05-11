# Bluetooth Device Protocol

This document describes the proposed communication protocol used by a Scratch Extension (or the extension framework) to
communicate with a Bluetooth Low Energy (BLE) peripheral's GATT interface using the Scratch Device Manager (SDM). This
document builds on the "Network Protocol" document describing the portions of the protocol common to all peripheral
types.

## Proposed Interface (Scratch Extension to Scratch Device Manager)

In general, BLE support in the SDM is patterned after BLE support in Web bluetooth. The Web Bluetooth specification
can be found here: https://webbluetoothcg.github.io/web-bluetooth/

### Initiating Communication with SDM

For BLE connections, an extension connects to the SDM’s WebSocket server at the path "/scratch/ble".

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

The "optionalServices" array, if present, shall contain service names which the Scratch Extension would like to access
even if they are not used for filtering. See the "Service Names" section below for more information on specifying
services in this list, or the "Connected State" section for more information about the relationship between the
"services" filter property, the "optionalServices" parameter, and the services available to the Scratch Extension.

Example JSON-RPC **request** sent from Scratch Extension to SDM to initiate discovery:
```json5
{
  "jsonrpc": "2.0",     // JSON-RPC version indicator
  "id": 1,              // Message sequence identifier
  "method": "discover", // Command identifier
  "params": {
    "filters": [
      { "name": "My Peripheral" },              // Accept device named exactly "My Peripheral"
      { "services": [ 0x1815, "current_time" ]} // Accept device with both "Automation IO" and "Current Time" services
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
- The "manufacturerData" and "serviceData" filter properties are not supported.

#### Service Names

The "services" array and, if present, the "optionalServices" array shall contain what the Web Bluetooth specification
calls "names" for GATT services. A GATT service name is one of the following:
- A full GATT service UUID in string format, such as "0000180f-0000-1000-8000-00805f9b34fb"
  - Hexadecimal characters shall be lower-case
- A "short ID" in integer format, such as 0x180f
- A name from the [Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services) without the
  "org.bluetooth.service." prefix, such as "battery_service"

Each of the examples above specifies the same service.

The SDM shall resolve each name to a full UUID using the
[getService](https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothuuid-getservice) algorithm as described by
the Web Bluetooth specification, which in practice is just shorthand for calling the
[resolveUuidName](https://webbluetoothcg.github.io/web-bluetooth/#resolveuuidname) algorithm and passing the
[Service Assigned Numbers table](https://www.bluetooth.com/specifications/gatt/services) and
"org.bluetooth.service" for the prefix.

### Connected State

#### Allowed Services

The SDM shall reject any attempt by the Scratch Extension to access a GATT service unless the service is specifically
allowed. A service is allowed if and only if:
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
      { "name": "My Peripheral" },              // Accept device named exactly "My Peripheral"
      { "services": [ 0x1815, "current_time" ]} // Accept device with both "Automation IO" and "Current Time" services
    ],
    "optionalServices": [
      "00001826-0000-1000-8000-00805f9b34fb"  // Allow the "Fitness Machine" service if present
    ]
  }
}
```

Suppose the SDM finds a peripheral with the name "My Peripheral" and reports that to the client in a
"didDiscoverPeripheral" notification, then the Scratch Extension chooses to connect to the "My Peripheral" device. The
Scratch Extension will be allowed to contact the following services:
- Service 0x1805, the "Current Time" service, with UUID 00001805-0000-1000-8000-00805f9b34fb
- Service 0x1815, the "Automation IO" service, with UUID 00001815-0000-1000-8000-00805f9b34fb
- Service 0x1826, the "Fitness Machine" service, with UUID 00001826-0000-1000-8000-00805f9b34fb

Note that the peripheral may or may not implement each of these services: in fact, since the peripheral satisfied the
discovery filter based on its name it might not implement any of these services.
