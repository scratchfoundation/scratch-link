import CoreBluetooth
import Foundation

class ScratchBLE: NSObject, CBCentralManagerDelegate {
    private let central: CBCentralManager
    private var sessions: [WebSocketSession]

    enum BluetoothError: Error {
        case NotReady
    }

    public var isReady: Bool {
        get {
            return central.state == .poweredOn
        }
    }

    override init() {
        central = CBCentralManager()
        sessions = []
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

    func scan(forSession wss: WebSocketSession, withOptions options: Any?) throws -> Codable? {
        if !isReady {
            throw BluetoothError.NotReady
        }

        print("I should scan for: \(String(describing:options))")

        central.scanForPeripherals(withServices: nil)

        if !sessions.contains(wss) {
            sessions.append(wss)
        }

        return nil
    }

    // Work around bug(?) in 10.13 SDK
    // see https://forums.developer.apple.com/thread/84375
    func getUUID(forPeripheral peripheral: CBPeripheral) -> UUID {
        return peripheral.value(forKey: "identifier") as! NSUUID as UUID
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral, advertisementData: [String: Any], rssi RSSI: NSNumber) {
        do {
            let peripheralData: [String: Any] = [
                "name": peripheral.name ?? "",
                "peripheralId": getUUID(forPeripheral: peripheral).uuidString,
                "RSSI": RSSI
            ]
            let responseJSON: [String:Any?] = [
                "jsonrpc": "2.0",
                "method": "didDiscoverPeripheral",
                "params": peripheralData
            ]

            let responseData = try JSONSerialization.data(withJSONObject: responseJSON)
            if let responseString = String(bytes: responseData, encoding: .utf8) {
                sessions.forEach {
                    session in
                    session.writeText(responseString)
                    print("Reporting discovered device to a session")
                }
            }
        } catch {
            print("Error handling discovered peripheral: \(error)")
        }
    }
}
