import CoreBluetooth
import Foundation
import Swifter

class BLESession: Session, SwiftCBCentralManagerDelegate, SwiftCBPeripheralDelegate {
    private static let MinimumSignalStrength:NSNumber = -70

    private let central: CBCentralManager
    private let centralDelegateHelper: CBCentralManagerDelegateHelper

    private let peripheralDelegateHelper: CBPeripheralDelegateHelper

    private var filters: [BLEScanFilter]?
    private var optionalServices: Set<CBUUID>?
    private var reportedPeripherals: [CBUUID:CBPeripheral]?
    private var allowedServices: Set<CBUUID>?

    private var connectedPeripheral: CBPeripheral?
    private var connectionCompletion: JSONRPCCompletionHandler?

    typealias CharacteristicDiscoveryCompletionHandler = (Error?) -> Void
    private var characteristicDiscoveryCompletion: [CBUUID:[CharacteristicDiscoveryCompletionHandler]]

    enum BluetoothError: Error {
        case NotReady
    }

    public var isReady: Bool {
        get {
            return central.state == .poweredOn
        }
    }

    required init(withSocket wss: WebSocketSession) {
        self.central = CBCentralManager()
        self.centralDelegateHelper = CBCentralManagerDelegateHelper()
        self.peripheralDelegateHelper = CBPeripheralDelegateHelper()
        self.characteristicDiscoveryCompletion = [:]
        super.init(withSocket: wss)
        self.centralDelegateHelper.delegate = self
        self.central.delegate = self.centralDelegateHelper
        self.peripheralDelegateHelper.delegate = self
    }

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        switch central.state {
        case .unknown:
            print("Bluetooth transitioned to unknown state")
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
        }
    }

    func discover(withParams params: [String:Any]) throws {
        guard let jsonFilters = params["filters"] as? [[String: Any]] else {
            throw JSONRPCError.InvalidParams(data: "could not parse filters in discovery request")
        }

        if jsonFilters.count < 1 {
            throw JSONRPCError.InvalidParams(data: "discovery request must include filters")
        }

        let newFilters = try jsonFilters.map({ try BLEScanFilter(fromJSON: $0) })

        if newFilters.contains(where: { $0.isEmpty }) {
            throw JSONRPCError.InvalidParams(data: "discovery request includes empty filter")
        }

        let newOptionalServices: Set<CBUUID>?
        if let jsonOptionalServices = params["optionalServices"] as? [String:Any] {
            newOptionalServices = Set<CBUUID>(try jsonOptionalServices.compactMap({
                guard let uuid = GATTHelpers.GetUUID(forService: $0) else {
                    throw JSONRPCError.InvalidParams(data: "could not resolve UUID for optional service \($0)")
                }
                return uuid
            }))
        } else {
            newOptionalServices = nil
        }

        var newAllowedServices = Set<CBUUID>(newOptionalServices ?? [])
        for filter in newFilters {
            if let filterServices = filter.RequiredServices {
                newAllowedServices.formUnion(filterServices)
            }
        }

        // TODO: wait for ready?
        if !isReady {
            throw BluetoothError.NotReady
        }

        connectedPeripheral = nil
        filters = newFilters
        optionalServices = newOptionalServices
        allowedServices = newAllowedServices
        reportedPeripherals = [:]
        central.scanForPeripherals(withServices: [CBUUID](allowedServices!))
    }

    // Work around bug(?) in 10.13 SDK
    // see https://forums.developer.apple.com/thread/84375
    func getUUID(forPeripheral peripheral: CBPeripheral) -> CBUUID {
        return CBUUID(nsuuid: peripheral.value(forKey: "identifier") as! NSUUID as UUID)
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral, advertisementData: [String: Any], rssi RSSI: NSNumber) {
        if RSSI.compare(BLESession.MinimumSignalStrength) == .orderedAscending {
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
            "RSSI": RSSI
        ]

        reportedPeripherals![uuid] = peripheral
        sendRemoteRequest("didDiscoverPeripheral", withParams: peripheralData)
    }

    func connect(withParams params: [String:Any], completion: @escaping JSONRPCCompletionHandler) throws {
        guard let peripheralIdString = params["peripheralId"] as? String else {
            throw JSONRPCError.InvalidParams(data: "missing or invalid peripheralId")
        }

        // if this fails to parse then we won't find the result in reportedPeripherals
        let peripheralId = CBUUID(string: peripheralIdString)

        guard let peripheral = reportedPeripherals?[peripheralId] else {
            throw JSONRPCError.InvalidParams(data: "invalid peripheralId: \(peripheralId)")
        }

        if connectionCompletion != nil {
            throw JSONRPCError.InvalidRequest(data: "connection already pending")
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
        if (peripheral != connectedPeripheral) {
            print("didDiscoverServices on wrong peripheral")
            return
        }

        guard let completion = connectionCompletion else {
            print("didDiscoverServices with no completion handler")
            return
        }

        if let error = error {
            completion(nil, JSONRPCError.ApplicationError(data: error.localizedDescription))
        } else {
            completion(nil, nil)
        }

        connectionCompletion = nil
    }

    func write(withParams params: [String:Any], completion: @escaping JSONRPCCompletionHandler) throws {
        let buffer = try EncodingHelpers.decodeBuffer(fromJSON: params)

        getEndpoint(for: "write request", withParams: params, blockedBy: .ExcludeWrites) { endpoint, error in
            if let error = error {
                completion(nil, error)
                return
            }

            guard let peripheral = self.connectedPeripheral else {
                // this should never happen
                completion(nil, JSONRPCError.InternalError(data: "write request without connected peripheral"))
                return
            }

            guard let endpoint = endpoint else {
                // this should never happen
                completion(nil, JSONRPCError.InternalError(data: "failed to find characteristic"))
                return
            }

            // TODO: allow client to specify write type?
            let writeType:CBCharacteristicWriteType =
                    (endpoint.properties.contains(.writeWithoutResponse)) ? .withoutResponse : .withResponse
            peripheral.writeValue(buffer, for: endpoint, type: writeType)
        }
    }

    typealias GetEndpointCompletionHandler = (_ result: CBCharacteristic?, _ error: JSONRPCError?) -> Void
    private func getEndpoint(
            for context:String, withParams params: [String: Any], blockedBy checkFlag: GATTBlockListStatus,
            completion: @escaping GetEndpointCompletionHandler) {
        guard let peripheral = connectedPeripheral else {
            completion(nil, JSONRPCError.InvalidRequest(data: "not connected for \(context)"))
            return
        }

        guard let serviceName = params["serviceId"] else {
            completion(nil, JSONRPCError.InvalidParams(data: "missing service UUID for \(context)"))
            return
        }

        guard let serviceId = GATTHelpers.GetUUID(forService: serviceName) else {
            completion(nil, JSONRPCError.InvalidParams(data: "could not determine service UUID for \(serviceName)"))
            return
        }

        if allowedServices?.contains(serviceId) != true {
            completion(nil, JSONRPCError.InvalidParams(data: "attempt to access unexpected service: \(serviceName)"))
            return
        }

        if let blockStatus = GATTHelpers.GetBlockListStatus(ofUUID: serviceId), blockStatus.contains(checkFlag) {
            completion(nil, JSONRPCError.InvalidParams(data: "service is block-listed with \(blockStatus): \(serviceName)"))
            return
        }

        guard let characteristicName = params["characteristicId"] else {
            completion(nil, JSONRPCError.InvalidParams(data: "missing characteristic UUID for \(context)"))
            return
        }

        guard let characteristicId = GATTHelpers.GetUUID(forCharacteristic: characteristicName) else {
            completion(nil, JSONRPCError.InvalidParams(data: "could not determine characteristic UUID for \(characteristicName)"))
            return
        }

        if let blockStatus = GATTHelpers.GetBlockListStatus(ofUUID: characteristicId) {
            completion(nil, JSONRPCError.InvalidParams(data: "characteristic is block-listed with \(blockStatus): \(characteristicName)"))
            return
        }

        guard let service = connectedPeripheral?.services?.first(where: {return $0.uuid == serviceId}) else {
            completion(nil, JSONRPCError.InvalidParams(data: "could not find service \(serviceName)"))
            return
        }

        func onCharacteristicsDiscovered(_ error: Error?) {
            if let error = error {
                completion(nil, JSONRPCError.ApplicationError(data: error.localizedDescription))
                return
            }

            guard let characteristic = service.characteristics?.first(where: {return $0.uuid == characteristicId}) else {
                completion(nil, JSONRPCError.InvalidParams(data: "could not find characteristic \(characteristicName) on service \(serviceName)"))
                return
            }

            completion(characteristic, nil)
        }

        if service.characteristics == nil {
            // There may be other handlers already waiting for discovery of characteristics on this service
            // Merge the new one-element list of handlers with any existing list which might be present
            characteristicDiscoveryCompletion.merge([serviceId: [onCharacteristicsDiscovered]]) { handlers1, handlers2 in
                var combinedHandlers = [CharacteristicDiscoveryCompletionHandler]()
                combinedHandlers.reserveCapacity(handlers1.count + handlers2.count)
                combinedHandlers.append(contentsOf: handlers1)
                combinedHandlers.append(contentsOf: handlers2)
                return combinedHandlers
            }
            peripheral.discoverCharacteristics(nil, for: service)
        } else {
            onCharacteristicsDiscovered(nil)
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

    override func didReceiveCall(_ method: String, withParams params: [String:Any],
                                 completion: @escaping JSONRPCCompletionHandler) throws {
        switch method {
        case "discover":
            try discover(withParams: params)
            completion(nil, nil)
        case "connect":
            try connect(withParams: params, completion: completion)
        case "write":
            try write(withParams: params, completion: completion)
        case "pingMe":
            completion("willPing", nil)
            sendRemoteRequest("ping") { (result: Any?, error: JSONRPCError?) in
                print("Got result from ping:", String(describing: result))
            }
        default:
            throw JSONRPCError.MethodNotFound(data: method)
        }
    }
}

struct BLEScanFilter {
    public let Name: String?
    public let NamePrefix: String?
    public let RequiredServices: Set<CBUUID>?

    public var isEmpty: Bool {
        get {
            return (Name?.isEmpty ?? true) && (NamePrefix?.isEmpty ?? true) && (RequiredServices?.isEmpty ?? true)
        }
    }

    // See https://webbluetoothcg.github.io/web-bluetooth/#bluetoothlescanfilterinit-canonicalizing
    init(fromJSON json: [String: Any]) throws {
        if let name = json["name"] as? String {
            Name = name
        } else {
            Name = nil
        }

        if let namePrefix = json["namePrefix"] as? String {
            NamePrefix = namePrefix
        }
        else {
            NamePrefix = nil
        }

        if let requiredServices = json["services"] as? [Any] {
            RequiredServices = Set<CBUUID>(try requiredServices.map({
                guard let uuid = GATTHelpers.GetUUID(forService: $0) else {
                    throw JSONRPCError.InvalidParams(data: "could not determine UUID for service \($0)")
                }
                return uuid
            }))
        } else {
            RequiredServices = nil
        }
    }

    // See https://webbluetoothcg.github.io/web-bluetooth/#matches-a-filter
    public func matches(_ peripheral: CBPeripheral, _ advertisementData: [String: Any]) -> Bool {
        if let peripheralName = peripheral.name {
            if let name = Name, !name.isEmpty, peripheralName != name {
                // peripheral name doesn't match filter name
                return false
            }

            if let namePrefix = NamePrefix, !namePrefix.isEmpty, !peripheralName.starts(with: namePrefix) {
                // peripheral name doesn't start with filter name prefix
                return false
            }
        } else {
            if !((Name?.isEmpty ?? true) && (NamePrefix?.isEmpty ?? true)) {
                // filter is looking for a name or name prefix but we don't have a name
                return false
            }
        }

        if let required = RequiredServices, !required.isEmpty {
            var available = Set<CBUUID>()
            if let services = peripheral.services {
                available.formUnion(services.map{$0.uuid})
            }
            if let serviceUUIDs = advertisementData["kCBAdvDataServiceUUIDs"] as? [CBUUID] {
                available.formUnion(serviceUUIDs)
            }
            return required.isSubset(of: available)
        }

        return true
    }
}
