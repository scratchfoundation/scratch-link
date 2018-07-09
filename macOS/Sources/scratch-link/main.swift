import Cocoa
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
class ScratchLink: NSObject, NSApplicationDelegate {
    let server: HttpServer = HttpServer()
    var sessionManagers = [SDMRoute: SessionManagerBase]()

    func applicationDidFinishLaunching(_ notification: Notification) {
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

    public func applicationWillTerminate(_ notification: Notification) {
        print("Good bye...")
    }
}

let application = NSApplication.shared
application.setActivationPolicy(.regular)

application.delegate = ScratchLink()
application.run()
