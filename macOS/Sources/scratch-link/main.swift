import Cocoa
import Foundation
import PerfectHTTP
import PerfectHTTPServer
import PerfectWebSockets

let SDMPort: Int = 20110

enum SDMRoute: String {
    case bluetoothLowEnergy = "/scratch/ble"
    case bluetooth = "/scratch/bt"
}

enum InitializationError: Error {
    case server(String)
}

enum SerializationError: Error {
    case invalid(String)
    case internalError(String)
}

// Provide Scratch access to hardware devices using a JSON-RPC 2.0 API over WebSockets.
// See NetworkProtocol.md for details.
class ScratchLink: NSObject, NSApplicationDelegate {
    var socketProtocol: String?

    var sessionManagers = [String: SessionManagerBase]()
    var sessions = [ObjectIdentifier: Session]()
    var statusBarItem: NSStatusItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        do {
            initUI()
            try initServer()
        } catch {
            print("Quitting due to initialization failure: \(error)")
            onQuitSelected()
        }
    }

    @objc
    private func onQuitSelected() {
        NSApplication.shared.terminate(nil)
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
            if let statusBarIcon = NSImage(
                named: NSImage.Name("iconTemplate")) ?? NSImage(named: NSImage.Name.caution) {
                button.image = statusBarIcon
            }
        }
        statusBarItem.menu = menu

        self.statusBarItem = statusBarItem

        // Hide the dock icon now that we have another way to quit
        var thisProcess = ProcessSerialNumber(highLongOfPSN: 0, lowLongOfPSN: UInt32(kCurrentProcess))
        TransformProcessType(&thisProcess, ProcessApplicationTransformState(kProcessTransformToUIElementApplication))
    }

    func initServer() throws {
        sessionManagers[SDMRoute.bluetoothLowEnergy.rawValue] = SessionManager<BLESession>()
        sessionManagers[SDMRoute.bluetooth.rawValue] = SessionManager<BTSession>()

        guard let certPath = Bundle.main.path(forResource: "scratch-device-manager", ofType: "pem") else {
            throw InitializationError.server("Failed to find certificate resource")
        }
        var routes = Routes()
        routes.add(method: .get, uri: "/scratch/*", handler: requestHandler)
        print("Starting server...")
        try HTTPServer.launch(wait: false, HTTPServer.Server(
            tlsConfig: TLSConfiguration(certPath: certPath),
            name: "device-manager.scratch.mit.edu",
            port: SDMPort,
            routes: routes
        ))
        print("server started")
    }

    func requestHandler(request: HTTPRequest, response: HTTPResponse) {
        print("request path: \(request.path)")
        if let sessionManager = sessionManagers[request.path] {
            do {
                try sessionManager
                    .makeSessionHandler(forRequest: request)
                    .handleRequest(request: request, response: response)
            } catch {
                response.setBody(string: "Session init failed")
                response.setHeader(.contentLength, value: "\(response.bodyBytes.count)")
                response.completed(status: .internalServerError)
            }
        } else {
            response.setBody(string: "Unrecognized path: \(request.path)")
            response.setHeader(.contentLength, value: "\(response.bodyBytes.count)")
            response.completed(status: .notFound)
        }
    }
}

let application = NSApplication.shared
application.setActivationPolicy(.regular)

let appDelegate = ScratchLink()
application.delegate = appDelegate

application.run()
