import Foundation
import Swifter

let SDMPort: in_port_t = 20110

enum SDMRoute: String {
    case BLE = "/scratch/ble"
    case BT = "/scratch/bt"
}

enum SerializationError: Error {
    case Invalid(String)
    case Internal(String)
}

// Provide Scratch access to hardware devices using a JSON-RPC 2.0 API over WebSockets.
// See NetworkProtocol.md for details.
class ScratchLink {
    let server: HttpServer
    var sessionManagers = [SDMRoute: SessionManagerBase]()

    init() {
        server = HttpServer()

        sessionManagers[SDMRoute.BLE] = SessionManager<BLESession>()
        sessionManagers[SDMRoute.BT] = SessionManager<BTSession>()

        server[SDMRoute.BLE.rawValue] = sessionManagers[SDMRoute.BLE]!.makeSocketHandler()
        server[SDMRoute.BT.rawValue] = sessionManagers[SDMRoute.BT]!.makeSocketHandler()

        print("Starting server...")
        do {
            try server.start(SDMPort)
            print("Server started")
        } catch let error {
            print("Failed to start server: \(error)")
        }
    }
}

let app = ScratchLink()

let runLoop = RunLoop.current
while runLoop.run(mode: .defaultRunLoopMode, before: .distantFuture) {
    // use select() to accept socket connections from tray icon / admin panel / something?
    print("Loop")
}
