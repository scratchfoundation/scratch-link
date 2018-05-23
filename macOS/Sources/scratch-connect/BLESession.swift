import CoreBluetooth
import Foundation
import Swifter

class BLESession: Session, SwiftCBCentralManagerDelegate {
    private static let MinimumSignalStrength:NSNumber = -70

    private let central: CBCentralManager
    private let delegateHelper: CBCentralManagerDelegateHelper

    private var filters: [BLEScanFilter]?
    private var optionalServices: Set<CBUUID>?
    private var reportedPeripherals: Set<CBUUID>?
    private var allowedServices: Set<CBUUID>?

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
        self.delegateHelper = CBCentralManagerDelegateHelper()
        super.init(withSocket: wss)
        self.delegateHelper.delegate = self
        self.central.delegate = self.delegateHelper
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
            newOptionalServices = Set<CBUUID>(try jsonOptionalServices.map{try GATTHelpers.GetUUID(forService: $0)})
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

        filters = newFilters
        optionalServices = newOptionalServices
        allowedServices = newAllowedServices
        reportedPeripherals = Set<CBUUID>()
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

        reportedPeripherals!.insert(uuid)
        sendRemoteRequest("didDiscoverPeripheral", withParams: peripheralData)
    }

    func connect(withParams params: [String:Any]) throws {
    }

    override func didReceiveCall(_ method: String, withParams params: [String:Any],
                                 completion: @escaping JSONRPCCompletionHandler) throws {
        switch method {
        case "discover":
            try discover(withParams: params)
            completion(nil, nil)
        case "connect":
            try connect(withParams: params)
            completion(nil, nil)
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
            RequiredServices = Set<CBUUID>(try requiredServices.map({ try GATTHelpers.GetUUID(forService: $0)}))
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
