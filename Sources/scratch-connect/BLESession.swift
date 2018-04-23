import CoreBluetooth
import Foundation
import Swifter

class BLESession: NSObject, Session, CBCentralManagerDelegate {
    private(set) var wss: WebSocketSession
    private let central = CBCentralManager()

    enum BluetoothError: Error {
        case NotReady
    }

    public var isReady: Bool {
        get {
            return central.state == .poweredOn
        }
    }

    required init(withSocket wss: WebSocketSession) {
        self.wss = wss
        super.init()
        central.delegate = self
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

    func discover(withOptions options: Any?) throws {
        if !isReady {
            throw BluetoothError.NotReady
        }

        print("I should scan for: \(String(describing:options))")

        central.scanForPeripherals(withServices: nil)
    }

    // Work around bug(?) in 10.13 SDK
    // see https://forums.developer.apple.com/thread/84375
    func getUUID(forPeripheral peripheral: CBPeripheral) -> UUID {
        return peripheral.value(forKey: "identifier") as! NSUUID as UUID
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral, advertisementData: [String: Any], rssi RSSI: NSNumber) {
        let peripheralData: [String: Any] = [
            "name": peripheral.name ?? "",
            "peripheralId": getUUID(forPeripheral: peripheral).uuidString,
            "RSSI": RSSI
        ]

        sendRemoteRequest("didDiscoverPeripheral", withParams: peripheralData)
    }

    func didReceiveCall(_ method: String, withParams params: [String:Any],
              completion: @escaping (_ result: Codable?, _ error: JSONRPCError?) -> Void) throws {
        switch method {
        case "discover":
            try discover(withOptions: params)
            completion(nil, nil)
        default:
            throw JSONRPCError.MethodNotFound(data: method)
        }
    }
}
