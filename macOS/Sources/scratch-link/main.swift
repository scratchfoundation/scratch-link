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
    case internalError(String)
}

enum SerializationError: Error {
    case invalid(String)
    case internalError(String)
}

extension HTTPServer.LaunchFailure {
    // dirty hack to access "message" member even though it's marked "internal"
    func getMessage() throws -> String {
        let mirror = Mirror(reflecting: self)
        for case let (label?, value) in mirror.children where label == "message" {
            if let messageString = value as? String {
                return messageString
            }
            throw InitializationError.internalError("Unexpected type for launch failure message")
        }
        throw InitializationError.internalError("Couldn't find launch failure message")
    }
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
            // Init server first: once we're a "UI Element Application" the NSAlert class no longer works correctly
            try initServer()
            initUI()
        } catch {
            print("Quitting due to initialization failure: \(error)")
            quit()
        }
    }

    private func quit() {
        NSApplication.shared.terminate(nil)
    }

    @objc
    private func onVersionItemSelected() {
        let versionDetails =
            """
            \(BundleInfo.getTitle()) \(BundleInfo.getVersion()) \(BundleInfo.getVersionDetail())
            macOS \(ProcessInfo().operatingSystemVersionString)
            """
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(versionDetails, forType: .string)

        let notification = NSUserNotification()
        notification.title = "Version information copied to clipboard"
        notification.informativeText = versionDetails
        NSUserNotificationCenter.default.deliver(notification)
    }

    @objc
    private func onQuitSelected() {
        quit()
    }

    func initUI() {
        let title = BundleInfo.getTitle()
        let versionItemText = "\(title) \(BundleInfo.getVersion())"

        let menu = NSMenu(title: title)
        menu.addItem(withTitle: versionItemText, action: #selector(onVersionItemSelected), keyEquivalent: "")
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit", action: #selector(onQuitSelected), keyEquivalent: "q")

        let systemStatusBar = NSStatusBar.system

        let statusBarItem = systemStatusBar.statusItem(withLength: NSStatusItem.squareLength)
        if let button = statusBarItem.button {
            button.imageScaling = .scaleProportionallyUpOrDown
            if let statusBarIcon = NSImage(
                named: NSImage.Name("iconTemplate")) ?? NSImage(named: NSImage.cautionName) {
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
        do {
            try HTTPServer.launch(wait: false, HTTPServer.Server(
                tlsConfig: TLSConfiguration(certPath: certPath),
                name: "device-manager.scratch.mit.edu",
                port: SDMPort,
                routes: routes
            ))
        } catch {
            try handleLaunchError(error)
        }
        print("Server started")
    }

    func does(string text: String, match regex: NSRegularExpression) -> Bool {
        let sourceRange = NSRange(text.startIndex..., in: text)
        let matchRange = regex.rangeOfFirstMatch(in: text, options: [], range: sourceRange)
        return matchRange.location != NSNotFound
    }

    func handleLaunchError(_ error: Error) throws {
        if let launchFailure = error as? HTTPServer.LaunchFailure {
            // TODO: it looks like Perfect throws away the real error code in HTTPServer's bindServer method.
            // Is there any way to get it back, maybe through the Server (not HTTPServer) object?
            // Is there a better way to catch the address-in-use case?
            let regexAddressInUse = try NSRegularExpression(
                pattern: "Another server was already listening on the requested port \\d+$"
            )
            if does(string: try launchFailure.getMessage(), match: regexAddressInUse) {
                onAddressInUse() // does not return
                return
            }
        }

        // None of the above handled the error. Re-throw it.
        throw error
    }

    func onAddressInUse() {
        let title = "Address already in use!"
        let body = (
            "\(BundleInfo.getTitle()) was unable to start because port \(SDMPort) is already in use.\n" +
            "\n" +
            "This means \(BundleInfo.getTitle()) is already running or another application is using that port.\n" +
            "\n" +
            "This application will now exit."
        )
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = body
        alert.alertStyle = .critical
        alert.addButton(withTitle: "OK")
        alert.runModal()
        quit()
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
