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

class ScratchBluetooth: NSObject, CBCentralManagerDelegate {
    private let central: CBCentralManager
    private var sessions: [WebSocketSession]

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

// Provide Scratch access to hardware devices using a JSON-RPC 2.0 API over WebSockets.
// See NetworkProtocol.md for details.
// TODO: implement remaining JSON-RPC 2.0 features: message batching, error responses
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
        guard let json = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] else {
            throw SerializationError.Invalid("top-level message structure")
        }

        // property "jsonrpc" must be exactly "2.0"
        if json["jsonrpc"] as? String != "2.0" {
            throw SerializationError.Invalid("JSON-RPC version string")
        }

        // If we made it this far, make sure we hear about this socket going away
        wss.delegate = self

        if json.keys.contains("method") {
            return try session(wss, didReceiveRequest: json)
        } else if json.keys.contains("result") || json.keys.contains("error") {
            return try session(wss, didReceiveResponse: json)
        } else {
            throw SerializationError.Invalid("message is neither request nor response")
        }
    }

    func session(_ wss: WebSocketSession, didReceiveRequest json: [String: Any]) throws -> Data? {
        guard let method = json["method"] as? String else {
            throw SerializationError.Invalid("method value missing or not a string")
        }

        // optional: dictionary of parameters by name
        // TODO: do we want to support passing parameters by position?
        let params: [String:Any] = (json["params"] as? [String:Any]) ?? [String:Any]()

        let result: Codable? = try call(method, forSession: wss, withParams: params)

        var response: [String:Any?] = [
            "jsonrpc": "2.0",
            "result": result
        ]
        if let id = json["id"] {
            response["id"] = id
        }
        return try JSONSerialization.data(withJSONObject: response)
    }

    func session(_ wss: WebSocketSession, didReceiveResponse json: [String: Any]) throws -> Data? {
        // TODO
        return nil
    }

    func sessionWillClose(_ session: WebSocketSession) {
        print("A session will close")
    }

    func call(_ method: String, forSession wss: WebSocketSession, withParams params: [String:Any]) throws -> Codable? {
        switch method {
        case "scan":
            return try bt.scan(forSession: wss, withOptions: params)
        default:
            print("Unknown method: \(method)")
            return nil
        }
    }
}

let app = ScratchConnect()

let runLoop = RunLoop.current
while runLoop.run(mode: .defaultRunLoopMode, before: .distantFuture) {
    // use select() to accept socket connections from tray icon / admin panel / something?
    print("Loop")
}
