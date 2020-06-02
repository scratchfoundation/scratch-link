import CoreBluetooth
import Foundation
import PerfectWebSockets

class BLESession: Session, SwiftCBCentralManagerDelegate, SwiftCBPeripheralDelegate {
    private static let MinimumSignalStrength = -70

    private let central: CBCentralManager
    private let centralDelegateHelper: CBCentralManagerDelegateHelper

    private let peripheralDelegateHelper: CBPeripheralDelegateHelper

    private var filters: [BLEScanFilter]?
    private var optionalServices: Set<CBUUID>?
    private var reportedPeripherals: [CBUUID: CBPeripheral]?
    private var allowedServices: Set<CBUUID>?

    private var connectedPeripheral: CBPeripheral?
    private var connectionCompletion: JSONRPCCompletionHandler?

    typealias DelegateHandler = (Error?) -> Void

    private var characteristicDiscoveryCompletion: [CBUUID: [DelegateHandler]]

    private var valueUpdateHandlers: [CBCharacteristic: [DelegateHandler]]
    private var watchedCharacteristics: Set<CBCharacteristic>

    private var onBluetoothReadyTasks: [(JSONRPCError?) -> Void]

    private enum BluetoothState {
        case unavailable
        case available
        case unknown
    }

    private var currentState: BluetoothState {
        switch central.state {
        case .unsupported: return .unavailable
        case .unauthorized: return .unavailable
        case .poweredOff: return .unavailable

        case .poweredOn: return .available

        case .resetting: return .unknown // probably the OS Bluetooth stack crashed and will "power on" again soon
        case .unknown: return .unknown
        @unknown default: return .unknown
        }
    }

    required init(withSocket webSocket: WebSocket) throws {
        self.central = CBCentralManager()
        self.centralDelegateHelper = CBCentralManagerDelegateHelper()
        self.peripheralDelegateHelper = CBPeripheralDelegateHelper()
        self.valueUpdateHandlers = [:]
        self.watchedCharacteristics = []
        self.characteristicDiscoveryCompletion = [:]
        self.onBluetoothReadyTasks = []
        try super.init(withSocket: webSocket)
        self.centralDelegateHelper.delegate = self
        self.central.delegate = self.centralDelegateHelper
        self.peripheralDelegateHelper.delegate = self
    }

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        // debugging
        switch central.state {
        case .resetting:
            print("Bluetooth is resetting")
        case .unsupported:
            print("Bluetooth is unsupported")
        case .unauthorized:
            print("Bluetooth is unauthorized")
        case .poweredOff:
            print("Bluetooth is now powered off")
        case .poweredOn:
            print("Bluetooth is now powered on")
        case .unknown: fallthrough
        @unknown default:
            print("Bluetooth transitioned to unknown state")
        }

        // actual work
        let btState = self.currentState

        if btState == .unknown {
            // just wait until the OS makes a decision
            return
        }

        let error = (btState == .unavailable) ?
            JSONRPCError.applicationError(data: "Bluetooth became unavailable: \(String(describing: central.state))") :
            nil

        while let task = onBluetoothReadyTasks.popLast() {
            task(error)
        }

        if let peripheral = self.connectedPeripheral {
            if btState != .available {
                // TODO: This call will probably fail with the message that CBCentralManager is not powered on,
                // which means the peripheral will stay "connected" until this session closes. The client should
                // close the session in response to the error we're about to send, but it would still be nice to
                // find a more reliable way to disconnect the peripheral here.
                central.cancelPeripheralConnection(peripheral)
                self.connectedPeripheral = nil
                do {
                    try self.sendErrorNotification(JSONRPCError.applicationError(data: "Bluetooth became unavailable"))
                } catch {
                    print("Failed to tell client that Bluetooth became unavailable: \(String(describing: error))")
                }
                self.sessionWasClosed()
            } else if peripheral.state != .connecting && peripheral.state != .connected {
                central.cancelPeripheralConnection(peripheral)
                self.connectedPeripheral = nil
                do {
                    try self.sendErrorNotification(JSONRPCError.applicationError(data: "Peripheral disconnected"))
                } catch {
                    print("Failed to tell client that the peripheral disconnected: \(String(describing: error))")
                }
                self.sessionWasClosed()
            }
        }
    }

    func discover(withParams params: [String: Any], completion: @escaping JSONRPCCompletionHandler) throws {
        guard let jsonFilters = params["filters"] as? [[String: Any]] else {
            throw JSONRPCError.invalidParams(data: "could not parse filters in discovery request")
        }

        if jsonFilters.count < 1 {
            throw JSONRPCError.invalidParams(data: "discovery request must include filters")
        }

        let newFilters = try jsonFilters.map({ try BLEScanFilter(fromJSON: $0) })

        if newFilters.contains(where: { $0.isEmpty }) {
            throw JSONRPCError.invalidParams(data: "discovery request includes empty filter")
        }

        let newOptionalServices: Set<CBUUID>?
        if let jsonOptionalServices = params["optionalServices"] as? [String] {
            newOptionalServices = Set<CBUUID>(try jsonOptionalServices.compactMap({
                guard let uuid = GATTHelpers.getUUID(forService: $0) else {
                    throw JSONRPCError.invalidParams(data: "could not resolve UUID for optional service \($0)")
                }
                return uuid
            }))
        } else {
            newOptionalServices = nil
        }

        var newAllowedServices = Set<CBUUID>(newOptionalServices ?? [])
        for filter in newFilters {
            if let filterServices = filter.requiredServices {
                newAllowedServices.formUnion(filterServices)
            }
        }

        func doDiscover(error: JSONRPCError?) {
            if let error = error {
                completion(nil, error)
            } else {
                connectedPeripheral = nil
                filters = newFilters
                optionalServices = newOptionalServices
                allowedServices = newAllowedServices
                reportedPeripherals = [:]
                central.scanForPeripherals(withServices: nil)

                completion(nil, nil)
            }
        }

        switch currentState {
        case .available: doDiscover(error: nil)
        case .unavailable:
            completion(nil, JSONRPCError.applicationError(
                data: "Bluetooth became unavailable: \(String(describing: central.state))"))
        case .unknown:
            onBluetoothReadyTasks.insert(doDiscover, at: 0)
        }
    }

    // Work around bug(?) in 10.13 SDK
    // see https://forums.developer.apple.com/thread/84375
    func getUUID(forPeripheral peripheral: CBPeripheral) -> CBUUID {
        // swiftlint:disable force_cast
        return CBUUID(nsuuid: peripheral.value(forKey: "identifier") as! NSUUID as UUID)
        // swiftlint:enable force_cast
    }

    // Canonicalizing into a full 128-bit UUID string using the canonicalUUID algorithm
    // see https://webbluetoothcg.github.io/web-bluetooth/#standardized-uuids
    private func getCanonicalUUIDString(uuid: String) -> String {
        var canonicalUUID = "0000" + uuid
        canonicalUUID = canonicalUUID + "-0000-1000-8000-00805f9b34fb"
        return canonicalUUID
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                        advertisementData: [String: Any], rssi rssiRaw: NSNumber) {
        let rssi = RSSI(rawValue: rssiRaw)
        if case .valid(let value) = rssi, value < BLESession.MinimumSignalStrength {
            // signal too weak
            return
        }

        if peripheral.state != .disconnected {
            // doesn't look like we could connect
            return
        }

        if filters?.contains(where: { return $0.matches(peripheral, advertisementData) }) != true {
            // no passing filters
            return
        }

        let uuid = getUUID(forPeripheral: peripheral)
        let peripheralData: [String: Any] = [
            "name": peripheral.name ?? "",
            "peripheralId": uuid.uuidString,
            "rssi": rssi.rawValue as Any
        ]

        reportedPeripherals![uuid] = peripheral
        sendRemoteRequest("didDiscoverPeripheral", withParams: peripheralData)
    }

    func connect(withParams params: [String: Any], completion: @escaping JSONRPCCompletionHandler) throws {
        guard let peripheralIdString = params["peripheralId"] as? String else {
            throw JSONRPCError.invalidParams(data: "missing or invalid peripheralId")
        }

        // if this fails to parse then we won't find the result in reportedPeripherals
        let peripheralId = CBUUID(string: peripheralIdString)

        guard let peripheral = reportedPeripherals?[peripheralId] else {
            throw JSONRPCError.invalidParams(data: "invalid peripheralId: \(peripheralId)")
        }

        if connectionCompletion != nil {
            throw JSONRPCError.invalidRequest(data: "connection already pending")
        }

        connectionCompletion = completion
        central.stopScan()
        central.connect(peripheral)
    }

    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        connectedPeripheral = peripheral

        // discover services before we report that we're connected
        // TODO: the documentation says "setting the parameter to nil is considerably slower and is not recommended"
        // but if I provide `allowedServices` then `peripheral.services` doesn't get populated...
        peripheral.delegate = peripheralDelegateHelper
        peripheral.discoverServices(nil)
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        if peripheral != connectedPeripheral {
            print("didDiscoverServices on wrong peripheral")
            return
        }

        guard let completion = connectionCompletion else {
            print("didDiscoverServices with no completion handler")
            return
        }

        if let error = error {
            completion(nil, JSONRPCError.applicationError(data: error.localizedDescription))
        } else {
            completion(nil, nil)
        }

        connectionCompletion = nil
    }

    func write(withParams params: [String: Any], completion: @escaping JSONRPCCompletionHandler) throws {
        let buffer = try EncodingHelpers.decodeBuffer(fromJSON: params)
        let withResponse = params["withResponse"] as? Bool

        getEndpoint(for: "write request", withParams: params, blockedBy: .ExcludeWrites) { endpoint, error in
            if let error = error {
                completion(nil, error)
                return
            }

            guard let peripheral = self.connectedPeripheral else {
                // this should never happen
                completion(nil, JSONRPCError.internalError(data: "write request without connected peripheral"))
                return
            }

            guard let endpoint = endpoint else {
                // this should never happen
                completion(nil, JSONRPCError.internalError(data: "failed to find characteristic"))
                return
            }

            // If the client specified a write type, honor that.
            // Otherwise, if the characteristic claims to support writing without response, do that.
            // Otherwise, write with response.
            let writeType = (withResponse ?? !endpoint.properties.contains(.writeWithoutResponse)) ?
                CBCharacteristicWriteType.withResponse : CBCharacteristicWriteType.withoutResponse
            peripheral.writeValue(buffer, for: endpoint, type: writeType)
            completion(buffer.count, nil)
        }
    }

    private func read(withParams params: [String: Any], completion: @escaping JSONRPCCompletionHandler) throws {
        let requestedEncoding = params["encoding"] as? String ?? "base64"
        let startNotifications = params["startNotifications"] as? Bool ?? false

        getEndpoint(for: "read request", withParams: params, blockedBy: .ExcludeReads) { endpoint, error in
            if let error = error {
                completion(nil, error)
                return
            }

            guard let peripheral = self.connectedPeripheral else {
                // this should never happen
                completion(nil, JSONRPCError.internalError(data: "read request without connected peripheral"))
                return
            }

            guard let endpoint = endpoint else {
                // this should never happen
                completion(nil, JSONRPCError.internalError(data: "failed to find characteristic"))
                return
            }

            self.addCallback(toRegistry: &self.valueUpdateHandlers, forKey: endpoint) { error in
                if let error = error {
                    completion(nil, JSONRPCError.applicationError(data: error.localizedDescription))
                    return
                }

                guard let value = endpoint.value else {
                    completion(nil, JSONRPCError.internalError(data: "failed to retrieve value of characteristic"))
                    return
                }

                guard let json = EncodingHelpers.encodeBuffer(value, withEncoding: requestedEncoding) else {
                    completion(nil, JSONRPCError.invalidRequest(
                            data: "failed to encode read result with \(requestedEncoding)"))
                    return
                }

                completion(json, nil)
            }

            if startNotifications {
                self.watchedCharacteristics.insert(endpoint)
                peripheral.setNotifyValue(true, for: endpoint)
            }

            peripheral.readValue(for: endpoint)
        }
    }

    private func startNotifications(withParams params: [String: Any],
                                    completion: @escaping JSONRPCCompletionHandler) {
        getEndpoint(for: "notification request", withParams: params, blockedBy: .ExcludeReads) { endpoint, error in
            if let error = error {
                completion(nil, error)
                return
            }

            guard let peripheral = self.connectedPeripheral else {
                // this should never happen
                completion(nil, JSONRPCError.internalError(data: "notification request without connected peripheral"))
                return
            }

            guard let endpoint = endpoint else {
                // this should never happen
                completion(nil, JSONRPCError.internalError(data: "failed to find characteristic"))
                return
            }

            self.watchedCharacteristics.insert(endpoint)
            peripheral.setNotifyValue(true, for: endpoint)

            completion(nil, nil)
        }
    }

    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        if peripheral != connectedPeripheral {
            print("didUpdateValueFor characteristic on wrong peripheral")
            return
        }

        if let handlers = valueUpdateHandlers.removeValue(forKey: characteristic) {
            for handler in handlers {
                handler(error)
            }
        }

        if watchedCharacteristics.contains(characteristic) {
            guard let value = characteristic.value else {
                print("failed to retrieve value of watched characteristic")
                return
            }

            // TODO: share this JSON with the handlers above to avoid encoding multiple times
            guard let json = EncodingHelpers.encodeBuffer(value, withEncoding: "base64") else {
                print("failed to encode value of watched characteristic")
                return
            }

            sendRemoteRequest("characteristicDidChange", withParams: json)
        }
    }

    private func stopNotifications(withParams params: [String: Any], completion: @escaping JSONRPCCompletionHandler) {
        getEndpoint(for: "stopNotifications request", withParams: params, blockedBy: .ExcludeReads) { endpoint, error in
            if let error = error {
                completion(nil, error)
            }

            guard let endpoint = endpoint else {
                completion(nil, JSONRPCError.invalidRequest(data: "failed to find characteristic"))
                return
            }

            self.watchedCharacteristics.remove(endpoint)

            endpoint.service.peripheral.setNotifyValue(false, for: endpoint)
        }
    }

    typealias GetEndpointCompletionHandler = (_ result: CBCharacteristic?, _ error: JSONRPCError?) -> Void
    private func getEndpoint(
            for context: String, withParams params: [String: Any], blockedBy checkFlag: GATTBlockListStatus,
            completion: @escaping GetEndpointCompletionHandler) {
        guard let peripheral = connectedPeripheral else {
            completion(nil, JSONRPCError.invalidRequest(data: "no peripheral for \(context)"))
            return
        }

        if peripheral.state != .connected {
            central.cancelPeripheralConnection(peripheral)
            connectedPeripheral = nil
            completion(nil, JSONRPCError.invalidRequest(data: "not connected for \(context)"))
            return
        }

        guard let serviceName = params["serviceId"] else {
            completion(nil, JSONRPCError.invalidParams(data: "missing service UUID for \(context)"))
            return
        }

        guard let serviceId = GATTHelpers.getUUID(forService: serviceName) else {
            completion(nil, JSONRPCError.invalidParams(data: "could not determine service UUID for \(serviceName)"))
            return
        }

        if allowedServices?.contains(serviceId) != true {
            completion(nil, JSONRPCError.invalidParams(data: "attempt to access unexpected service: \(serviceName)"))
            return
        }

        if let blockStatus = GATTHelpers.getBlockListStatus(ofUUID: serviceId), blockStatus.contains(checkFlag) {
            completion(nil, JSONRPCError.invalidParams(
                data: "service is block-listed with \(blockStatus): \(serviceName)"))
            return
        }

        guard let characteristicName = params["characteristicId"] else {
            completion(nil, JSONRPCError.invalidParams(data: "missing characteristic UUID for \(context)"))
            return
        }

        guard let characteristicId = GATTHelpers.getUUID(forCharacteristic: characteristicName) else {
            completion(nil, JSONRPCError.invalidParams(
                data: "could not determine characteristic UUID for \(characteristicName)"))
            return
        }

        if let blockStatus = GATTHelpers.getBlockListStatus(ofUUID: characteristicId) {
            completion(nil, JSONRPCError.invalidParams(
                data: "characteristic is block-listed with \(blockStatus): \(characteristicName)"))
            return
        }

        guard let service = connectedPeripheral?.services?.first(where: {return $0.uuid == serviceId}) else {
            completion(nil, JSONRPCError.invalidParams(data: "could not find service \(serviceName)"))
            return
        }

        func onCharacteristicsDiscovered(_ error: Error?) {
            if let error = error {
                completion(nil, JSONRPCError.applicationError(data: error.localizedDescription))
                return
            }

            guard let characteristic = service.characteristics?.first(
                where: {return $0.uuid == characteristicId}) else {
                completion(nil, JSONRPCError.invalidParams(
                    data: "could not find characteristic \(characteristicName) on service \(serviceName)"))
                return
            }

            completion(characteristic, nil)
        }

        if service.characteristics == nil {
            addCallback(
                    toRegistry: &characteristicDiscoveryCompletion,
                    forKey: serviceId,
                    callback: onCharacteristicsDiscovered)
            peripheral.discoverCharacteristics(nil, for: service)
        } else {
            onCharacteristicsDiscovered(nil)
        }
    }

    func addCallback<T, U>(toRegistry registry: inout [T: [U]], forKey key: T, callback: U) {
        if var handlers = registry[key] {
            handlers.append(callback)
        } else {
            registry[key] = [callback]
        }
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        guard let handlers = characteristicDiscoveryCompletion.removeValue(forKey: service.uuid) else {
            print("didDiscoverCharacteristicsFor service but found no handlers")
            return
        }

        for handler in handlers {
            handler(error)
        }
    }

    override func didReceiveCall(_ method: String, withParams params: [String: Any],
                                 completion: @escaping JSONRPCCompletionHandler) throws {
        switch method {
        case "discover":
            try discover(withParams: params, completion: completion)
        case "connect":
            try connect(withParams: params, completion: completion)
        case "write":
            try write(withParams: params, completion: completion)
        case "read":
            try read(withParams: params, completion: completion)
        case "startNotifications":
            startNotifications(withParams: params, completion: completion)
        case "stopNotifications":
            stopNotifications(withParams: params, completion: completion)
        case "getServices":
            var services = [String]()
            connectedPeripheral?.services?.forEach{
                services.append(getCanonicalUUIDString(uuid: $0.uuid.uuidString))
            }
            completion(services, nil)
        default:
            // unrecognized method: pass to base class
            try super.didReceiveCall(method, withParams: params, completion: completion)
        }
    }
}

struct BLEScanFilter {
    public let name: String?
    public let namePrefix: String?
    public let requiredServices: Set<CBUUID>?
    public let manufacturerData: [UInt16:[String:[UInt8]]]?
    public var isEmpty: Bool {
        return (name?.isEmpty ?? true) && (namePrefix?.isEmpty ?? true) && (requiredServices?.isEmpty ?? true) && (manufacturerData?.isEmpty ?? true)
    }

    // See https://webbluetoothcg.github.io/web-bluetooth/#bluetoothlescanfilterinit-canonicalizing
    init(fromJSON json: [String: Any]) throws {
        if let name = json["name"] as? String {
            self.name = name
        } else {
            self.name = nil
        }

        if let namePrefix = json["namePrefix"] as? String {
            self.namePrefix = namePrefix
        } else {
            self.namePrefix = nil
        }

        if let requiredServices = json["services"] as? [Any] {
            self.requiredServices = Set<CBUUID>(try requiredServices.map({
                guard let uuid = GATTHelpers.getUUID(forService: $0) else {
                    throw JSONRPCError.invalidParams(data: "could not determine UUID for service \($0)")
                }
                return uuid
            }))
        } else {
            self.requiredServices = nil
        }

        if let manufacturerData = json["manufacturerData"] as? [String: Any] {
            // Javascript sends over object-indexes as strings, so it's necessary to cast to the proper datatypes
            var dict = [UInt16: [String: [UInt8]]]()
            for (k, v) in manufacturerData {
                // Make sure that manufacturerData is [UInt16: [String: [UInt8]]]
                guard let key = UInt16(k), var values = v as? [String: [UInt8]] else {
                    throw JSONRPCError.invalidParams(data: "could not parse manufacturer data")
                }

                guard let dataPrefix = values["dataPrefix"] else {
                    throw JSONRPCError.invalidParams(data: "no data prefix specified")
                }

                // If no mask is supplied, create a mask matching the length of dataPrefix
                let mask = (values["mask"] ?? [UInt8](Array(repeating: 0xFF, count: dataPrefix.count)))
                values["mask"] = mask

                if dataPrefix.count != mask.count {
                    throw JSONRPCError.invalidParams(data: "length of data prefix does not match length of mask")
                }

                dict[key] = values
            }
            self.manufacturerData = dict
        } else {
            self.manufacturerData = nil
        }
    }

    // See https://webbluetoothcg.github.io/web-bluetooth/#matches-a-filter
    public func matches(_ peripheral: CBPeripheral, _ advertisementData: [String: Any]) -> Bool {
        if let peripheralName = peripheral.name {
            if let name = name, !name.isEmpty, peripheralName != name {
                // peripheral name doesn't match filter name
                return false
            }

            if let namePrefix = namePrefix, !namePrefix.isEmpty, !peripheralName.starts(with: namePrefix) {
                // peripheral name doesn't start with filter name prefix
                return false
            }
        } else {
            if !((name?.isEmpty ?? true) && (namePrefix?.isEmpty ?? true)) {
                // filter is looking for a name or name prefix but we don't have a name
                return false
            }
        }

        if let required = requiredServices, !required.isEmpty {
            var available = Set<CBUUID>()
            if let services = peripheral.services {
                available.formUnion(services.map {$0.uuid})
            }
            if let serviceUUIDs = advertisementData["kCBAdvDataServiceUUIDs"] as? [CBUUID] {
                available.formUnion(serviceUUIDs)
            }
            if !required.isSubset(of: available) {
                return false
            }
        }

        if let manufacturer = manufacturerData, !manufacturer.isEmpty {
            // TODO: figure out whether it's possible to have a device with two manufacturerData items
            // if so, fix this code to handle that
            for i in manufacturer {
                // check if a prefix and mask have been supplied by the extension and that their lengths match
                let id = i.key
                guard let prefix = i.value["dataPrefix"], let mask = i.value["mask"] else {
                    return false
                }
                // create an array that is the result of the prefix and mask AND'ed
                let maskedPrefix = prefix.enumerated().map { (key, value) in
                    return value & mask[key]
                }
                // if discovered device has ManufacturerData, take the first slice and AND it with the mask
                // return true if the masked prefix from the extension matches the masked data supplied by the device
                if let deviceData = advertisementData[CBAdvertisementDataManufacturerDataKey] as? Data {
                    // take two first bytes of advertisementData and use as Device ID
                    let deviceId = UInt16(deviceData[0]) | UInt16(deviceData[1]) << 8;
                    // take remaining number of bytes equal to the length of the mask to be used for comparison
                    let devicePrefix = [UInt8](deviceData).dropFirst(2).prefix(mask.count)

                    let maskedDevice = devicePrefix.enumerated().map { (key, value) in
                        return value & mask[key]
                    }
                    if deviceId != id || maskedPrefix != maskedDevice {
                        return false
                    }
                } else {
                    // no manufacturerData available from device
                    return false
                }
            }
        }

        // nothing failed so the filter as a whole matches
        return true
    }
}
