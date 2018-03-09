import CoreBluetooth
import Foundation
import Swifter

let SDMPort: in_port_t = 20110

enum SDMRoutes: String {
    case BLE = "/scratch/ble"
}

enum SerializationError: Error {
    case Invalid(String)
    case Internal(String)
}

enum BluetoothError: Error {
    case NotReady
}

@available(OSX 10.13, *)
class ScratchBluetooth: NSObject, CBCentralManagerDelegate {
    private let central: CBCentralManager
    private var sessions: [WebSocketSession]
    private var callNumber: Int

    public var isReady: Bool {
        get {
            return central.state == .poweredOn
        }
    }

    override init() {
        callNumber = 0
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

    func scan(forSession wss: WebSocketSession, withOptions options: Any?) throws -> Any? {
        if !isReady {
            throw BluetoothError.NotReady
        }

        print("I should scan for: \(options)")

        central.scanForPeripherals(withServices: nil)

        if !sessions.contains(wss) {
            sessions.append(wss)
        }

        return "scan started"
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral, advertisementData: [String: Any], rssi RSSI: NSNumber) {
        do {
            let objectJSON: [String: Any] = [
                "name": peripheral.name ?? "",
                "UUID": peripheral.identifier.uuidString,
                "RSSI": RSSI
            ]
            let responseJSON: [Any] = [
                (callNumber += 1),
                "didDiscoverPeripheral",
                objectJSON
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

@available(OSX 10.13, *)
class ScratchConnect: WebSocketSessionDelegate {
    let server: HttpServer
    let bt: ScratchBluetooth

    init() {
        server = HttpServer()
        bt = ScratchBluetooth()

        server[SDMRoutes.BLE.rawValue] = websocket(session(_:didReceiveText:), session(_:didReceiveBinary:))

        print("Starting server...")
        do {
            try server.start(SDMPort)
            print("Server started")
        } catch let error {
            print("Failed to start server: \(error)")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveText text: String) {
        do {
            guard let data = text.data(using: .utf8) else {
                throw SerializationError.Internal("text decoding")
            }
            if let result = try session(wss, didReceiveJSON: data) {
                guard let jsonReply = String(bytes: result, encoding: .utf8) else {
                    throw SerializationError.Internal("reply encoding")
                }
                wss.writeText(jsonReply)
            }
        } catch {
            print("Error handling text message: \(error)")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveBinary data: [UInt8]) {
        do {
            if let result = try session(wss, didReceiveJSON: Data(data)) {
                let jsonReply = [UInt8](result)
                wss.writeBinary(jsonReply)
            }
        } catch let error {
            print("Error handling binary message: \(error)")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveJSON data: Data) throws -> Data? {
        guard let json = try JSONSerialization.jsonObject(with: data, options: []) as? [Any] else {
            throw SerializationError.Invalid("top-level message structure")
        }

        let callbackToken = json[0]
        guard let action = json[1] as? String else {
            throw SerializationError.Invalid("action identifier")
        }

        // If we made it this far, make sure we hear about this socket going away
        wss.delegate = self

        let reply = { () -> [Any] in
            do {
                if let result = try performAction(action, forSession: wss, withArgs: Array(json[2...])) {
                    return [callbackToken, "@", result]
                } else {
                    return [callbackToken, "@"]
                }
            } catch {
                // TODO: we shouldn't send back the whole error - we might leak sensitive info
                return [callbackToken, "!", error]
            }
        }()
        return try JSONSerialization.data(withJSONObject: reply)
    }

    func sessionWillClose(_ session: WebSocketSession) {
        print("A session will close")
    }

    func performAction(_ action: String, forSession wss: WebSocketSession, withArgs args: [Any]) throws -> Any? {
        switch action {
        case "scan":
            return try bt.scan(forSession: wss, withOptions: args[0])
        default:
            print("Unknown action: \(action)")
            return nil
        }
    }
}

if #available(OSX 10.13, *) {
    let app = ScratchConnect()

    let runLoop = RunLoop.current
    while runLoop.run(mode: .defaultRunLoopMode, before: .distantFuture) {
        // use select() to accept socket connections from tray icon / admin panel / something?
        print("Loop")
    }
} else {
    // Fallback on earlier versions
}
