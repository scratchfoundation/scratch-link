import Cocoa
import Foundation
import Telegraph

let SDMPort: in_port_t = 20110

enum SDMRoute: String {
    case BLE = "/scratch/ble"
    case BT = "/scratch/bt"
}

enum InitializationError: Error {
    case Server(String)
}

enum SerializationError: Error {
    case Invalid(String)
    case Internal(String)
}

// Provide Scratch access to hardware devices using a JSON-RPC 2.0 API over WebSockets.
// See NetworkProtocol.md for details.
class ScratchLink: NSObject, NSApplicationDelegate, ServerWebSocketDelegate {
    var server: Server?
    var sessionManagers = [String: SessionManagerBase]()
    var sessions = [ObjectIdentifier: Session]()
    var statusBarItem: NSStatusItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        do {
            initUI()
            try initServer()
        } catch let e {
            print("Quitting due to initialization failure: \(e)")
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

    func initServer() throws {
        let caChain = try ["ca", "int"].map { name -> Certificate in
            guard let url = Bundle.main.url(forResource: name, withExtension: "der") else {
                throw InitializationError.Server("Could not build path for certificate: \(name)")
            }
            guard let certificate = Certificate(derURL: url) else {
                throw InitializationError.Server("Coult not load certificate: \(name)")
            }
            return certificate
        }
        guard let idCertificatePath = Bundle.main.url(forResource: "scratch-device-manager", withExtension: "pfx") else {
            throw InitializationError.Server("Could not build ID certificate path")
        }
        guard let idCertificate = CertificateIdentity(p12URL: idCertificatePath, passphrase: "Scratch") else {
            throw InitializationError.Server("Could not load ID certificate")
        }

        let server = Server(identity: idCertificate, caCertificates: caChain)
        self.server = server
        server.webSocketDelegate = self

        sessionManagers[SDMRoute.BLE.rawValue] = SessionManager<BLESession>()
        sessionManagers[SDMRoute.BT.rawValue] = SessionManager<BTSession>()

        print("Starting server...")
        do {
            try server.start(onPort: SDMPort)
            print("Server started")
        } catch let error {
            print("Failed to start server: \(error)")
        }
    }

    func server(_ server: Server, webSocketDidConnect webSocket: WebSocket, handshake: HTTPRequest) {
        print(handshake.uri.path)
        if let sessionManager = sessionManagers[handshake.uri.path] {
            if let session = try? sessionManager.makeSession(forSocket: webSocket) {
                sessions[ObjectIdentifier(webSocket)] = session
            } else {
                webSocket.send(text: "Error making session for connection at path: \(handshake.uri.path)")
                webSocket.close(immediately: false)
            }
        } else {
            webSocket.send(text: "Unrecognized path: \(handshake.uri.path)")
            webSocket.close(immediately: false)
        }
    }

    func server(_ server: Server, webSocketDidDisconnect webSocket: WebSocket, error: Error?) {
        if let error = error {
            print("WebSocket disconnecting due to error: \(error)")
        } else {
            print("WebSocket disconnecting without error")
        }
        if let session = sessions.removeValue(forKey: ObjectIdentifier(webSocket)) {
            session.sessionWasClosed()
        }
    }

    func server(_ server: Server, webSocket: WebSocket, didReceiveMessage message: WebSocketMessage) {
        if let session = sessions[ObjectIdentifier(webSocket)] {
            session.didReceiveMessage(message)
        } else {
            webSocket.send(text: "No session for this socket")
            webSocket.close(immediately: false)
            print("Closing WebSocket on unrecognized path")
        }
    }

    func server(_ server: Server, webSocket: WebSocket, didSendMessage message: WebSocketMessage) {
        // do nothing
    }

    func serverDidDisconnect(_ server: Server) {
        print("Server disconnecting")
    }
}

let application = NSApplication.shared
application.setActivationPolicy(.regular)

let appDelegate = ScratchLink()
application.delegate = appDelegate

application.run()
