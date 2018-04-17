import Foundation
import Swifter

let SDMPort: in_port_t = 20110

enum SDMRoute: String {
    case BLE = "/scratch/ble"
    case BT = "/scratch/bt" // should this be BT, RFCOMM, ...?
}

enum SerializationError: Error {
    case Invalid(String)
    case Internal(String)
}

// Provide Scratch access to hardware devices using a JSON-RPC 2.0 API over WebSockets.
// See NetworkProtocol.md for details.
class ScratchConnect {
    let server: HttpServer
    var sessionManagers = [SDMRoute: SessionManagerBase]()

    init() {
        server = HttpServer()

        sessionManagers[SDMRoute.BLE] = SessionManager<BLESession>()

        server[SDMRoute.BLE.rawValue] = sessionManagers[SDMRoute.BLE]!.makeSocketHandler()

        print("Starting server...")
        do {
            try server.start(SDMPort)
            print("Server started")
        } catch let error {
            print("Failed to start server: \(error)")
        }
    }
}

protocol Session {
    // Override this in your hardware-specific session
    func call(_ method: String, withParams params: [String:Any]) throws -> Codable?
    init(withSocket wss: WebSocketSession)

    // These are implemented for you in the extension
    func didReceiveRequest(_ json: [String: Any]) throws -> Data?
    func didReceiveResponse(_ json: [String: Any]) throws -> Data?
}

extension Session {
    func didReceiveRequest(_ json: [String: Any]) throws -> Data? {
        guard let method = json["method"] as? String else {
            throw SerializationError.Invalid("method value missing or not a string")
        }

        // optional: dictionary of parameters by name
        // TODO: do we want to support passing parameters by position?
        let params: [String:Any] = (json["params"] as? [String:Any]) ?? [String:Any]()

        let result: Codable? = try call(method, withParams: params)

        var response: [String:Any?] = [
            "jsonrpc": "2.0",
            "result": result
        ]
        if let id = json["id"] {
            response["id"] = id
        }
        return try JSONSerialization.data(withJSONObject: response)
    }

    func didReceiveResponse(_ json: [String: Any]) throws -> Data? {
        // TODO
        return nil
    }
}

let app = ScratchConnect()

let runLoop = RunLoop.current
while runLoop.run(mode: .defaultRunLoopMode, before: .distantFuture) {
    // use select() to accept socket connections from tray icon / admin panel / something?
    print("Loop")
}
