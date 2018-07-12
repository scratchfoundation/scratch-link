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
    var statusBarItem: NSStatusItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        initUI()
        initServer()
    }

    func initUI() {
        let menu = NSMenu(title: "Scratch Link")
        menu.addItem(withTitle: "Scratch Link", action: nil, keyEquivalent: "")
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit", action: #selector(onQuitSelected), keyEquivalent: "q")

        let systemStatusBar = NSStatusBar.system

        let statusBarItem = systemStatusBar.statusItem(withLength: NSStatusItem.squareLength)
        if let button = statusBarItem.button {
            button.imageScaling = .scaleProportionallyUpOrDown
            if let statusBarIcon = NSImage(named: NSImage.Name("iconTemplate")) ?? NSImage(named: NSImage.Name.caution) {
                button.image = statusBarIcon
            }
        }
        statusBarItem.menu = menu

        self.statusBarItem = statusBarItem

        // Hide the dock icon now that we have another way to quit
        var thisProcess = ProcessSerialNumber(highLongOfPSN: 0, lowLongOfPSN: UInt32(kCurrentProcess))
        TransformProcessType(&thisProcess, ProcessApplicationTransformState(kProcessTransformToUIElementApplication))
    }

    func initServer() {
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

    @objc
    private func onQuitSelected() {
        NSApplication.shared.terminate(nil)
    }
}

let application = NSApplication.shared
application.setActivationPolicy(.regular)

let appDelegate = ScratchLink()
application.delegate = appDelegate

application.run()
