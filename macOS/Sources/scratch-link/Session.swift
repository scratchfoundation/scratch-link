import Foundation
import PerfectHTTP
import PerfectWebSockets

// TODO: implement remaining JSON-RPC 2.0 features like batching
class Session {
    // Keep this in sync with the version number in `NetworkProtocol.md`
    private let NetworkProtocolVersion: String = "1.2"

    typealias RequestID = Int
    typealias JSONRPCCompletionHandler = (_ result: Any?, _ error: JSONRPCError?) -> Void

    let socketProtocol: String? = nil // must match client sub-protocol
    private let webSocket: WebSocket
    private var nextId: RequestID = 0
    private var completionHandlers = [RequestID: JSONRPCCompletionHandler]()

    private let socketReadSemaphore = DispatchSemaphore(value: 1)
    private let socketWriteSemaphore = DispatchSemaphore(value: 1)
    private let sessionSemaphore = DispatchSemaphore(value: 1)

    required init(withSocket webSocket: WebSocket) throws {
        self.webSocket = webSocket
    }

    func handleSession(webSocket: WebSocket) {
        var message = ""
        var keepGoing = true

        while keepGoing {
            socketReadSemaphore.wait()
            // Perfect will automatically convert binary messages to look like text messages
            // TODO: consider inspecting `op` for text/binary so we can send a matched response
            webSocket.readStringMessage { text, _, isFinal in
                self.socketReadSemaphore.signal()
                guard let text = text else {
                    // This block will be executed if, for example, the browser window is closed.
                    keepGoing = false
                    self.sessionWasClosed()
                    return
                }
                message.append(contentsOf: text)
                if isFinal {
                    let wholeMessage = message
                    message = ""
                    self.didReceiveText(wholeMessage)
                }
            }
        }
    }

    // Override this to clean up session-specific resources, if any.
    func sessionWasClosed() {
        self.sessionSemaphore.mutex {
            if completionHandlers.count > 0 {
                print("Warning: session was closed with \(completionHandlers.count) pending requests")
                for (_, completionHandler) in completionHandlers {
                    completionHandler(nil, JSONRPCError.internalError(data: "Session closed"))
                }
            }
            self.webSocket.close()
        }
    }

    func didReceiveText(_ text: String) {
        guard let messageData = text.data(using: .utf8) else {
            print("Failed to convert client text to UTF8")
            return
        }
        sessionSemaphore.wait()
        didReceiveData(messageData) { jsonResponseData in
            self.sessionSemaphore.signal()

            if let jsonResponseData = jsonResponseData {
                if let jsonResponseText = String(data: jsonResponseData, encoding: .utf8) {
                    self.socketWriteSemaphore.wait()
                    self.webSocket.sendStringMessage(string: jsonResponseText, final: true) {
                        self.socketWriteSemaphore.signal()
                    }
                } else {
                    print("Failed to decode response")
                }
            }
        }
    }

    // Override this to handle received RPC requests & notifications.
    // Call this method with `await super.DidReceiveCall(...)` to implement default calls like `getVersion`.
    // Call the completion handler when done with a request:
    // - pass your call's "return value" (or nil) as `result` on success
    // - pass an instance of `JSONRPCError` for `error` on failure
    // You may also throw a `JSONRPCError` (or any other `Error`) iff it is encountered synchronously.
    func didReceiveCall(_ method: String, withParams params: [String: Any],
                        completion: @escaping JSONRPCCompletionHandler) throws {
        switch method {
        case "pingMe":
            completion("willPing", nil)
            sendRemoteRequest("ping") { (result: Any?, _: JSONRPCError?) in
                print("Got result from ping:", String(describing: result))
            }
        case "getVersion":
            completion(getVersion(), nil)
        default:
            throw JSONRPCError.methodNotFound(data: method)
        }
    }

    // Create an associative array containing version information. All version values must be strings.
    // This base version puts the network protocol version in a property called `protocol`.
    // Subclasses may choose to override this method to add more info; the recommended pattern is:
    //   var versionInfo = super.getVersion()
    //   versionInfo["mySpecialVersion"] = someValue
    //   return versionInfo
    func getVersion() -> [String: String] {
        let versionInfo: [String: String] = [
            "protocol": NetworkProtocolVersion
        ]
        return versionInfo
    }

    // Pass nil for the completion handler to send a Notification
    // Note that the closure is automatically @escaping by virtue of being part of an aggregate (Optional)
    func sendRemoteRequest(_ method: String, withParams params: [String: Any]? = nil,
                           completion: JSONRPCCompletionHandler? = nil) {
        var request: [String: Any?] = [
            "jsonrpc": "2.0",
            "method": method
        ]

        if params != nil {
            request["params"] = params
        }

        if completion != nil {
            let requestId = getNextId()
            completionHandlers[requestId] = completion
            request["id"] = requestId
        }

        do {
            let requestData = try JSONSerialization.data(withJSONObject: request)
            guard let requestText = String(data: requestData, encoding: .utf8) else {
                throw SerializationError.internalError("Could not serialize request before sending to client")
            }
            self.socketWriteSemaphore.wait()
            self.webSocket.sendStringMessage(string: requestText, final: true) {
                self.socketWriteSemaphore.signal()
            }
        } catch {
            print("Error serializing request JSON: \(error)")
            print("Request was: \(request)")
        }
    }

    func sendErrorNotification(_ error: JSONRPCError) throws {
        let message = makeResponse(forId: NSNull(), nil, error)
        let messageData = try JSONSerialization.data(withJSONObject: message)
        guard let messageText = String(data: messageData, encoding: .utf8) else {
            throw SerializationError.internalError("Could not serialize error before sending to client")
        }
        self.socketWriteSemaphore.wait()
        self.webSocket.sendStringMessage(string: messageText, final: true) {
            self.socketWriteSemaphore.signal()
        }
    }

    func makeResponse(forId responseId: Any, _ result: Any?, _ error: JSONRPCError?) -> [String: Any] {
        var response: [String: Any] = [
            "jsonrpc": "2.0"
        ]
        response["id"] = responseId
        if let error = error {
            var jsonError: [String: Any] = [
                "code": error.code,
                "message": error.message
            ]
            if let data = error.data {
                jsonError["data"] = data
            }
            response["error"] = jsonError
        } else {
            // If there's no error then we must include this as a success flag, even if the value is null
            response["result"] = result ?? NSNull()
        }

        return response
    }

    func didReceiveData(_ data: Data, completion: @escaping (_ jsonResponseData: Data?) -> Void) {
        var responseId: Any = NSNull() // initialize with null until we try to read the real ID

        func sendResponse(_ result: Any?, _ error: JSONRPCError?) {
            do {
                let response = makeResponse(forId: responseId, result, error)
                let jsonData = try JSONSerialization.data(withJSONObject: response)
                completion(jsonData)
            } catch let firstError {
                do {
                    let errorResponse = makeResponse(forId: responseId, nil, JSONRPCError(
                            code: 2, message: "Could not encode response", data: String(describing: firstError)))
                    let jsonData = try JSONSerialization.data(withJSONObject: errorResponse)
                    completion(jsonData)
                } catch let secondError {
                    print("Failure to report failure to encode!")
                    print("Initial error: \(String(describing: firstError))")
                    print("Secondary error: \(String(describing: secondError))")
                    completion(nil)
                }
            }
        }

        do {
            guard let json = try JSONSerialization.jsonObject(with: data, options: []) as? [String: Any] else {
                throw JSONRPCError.parseError(data: "unrecognized message structure")
            }

            // do this as early as possible so that error responses can include it. If it's not present, use null.
            responseId = json["id"] ?? NSNull()

            // property "jsonrpc" must be exactly "2.0"
            if json["jsonrpc"] as? String != "2.0" {
                throw JSONRPCError.invalidRequest(data: "unrecognized JSON-RPC version string")
            }

            if json.keys.contains("method") {
                try didReceiveRequest(json, completion: sendResponse)
            } else if json.keys.contains("result") || json.keys.contains("error") {
                try didReceiveResponse(json)
                completion(nil)
            } else {
                throw JSONRPCError.invalidRequest(data: "message is neither request nor response")
            }
        } catch let error where error is JSONRPCError {
            sendResponse(nil, error as? JSONRPCError)
        } catch {
            sendResponse(nil, JSONRPCError(
                    code: 1, message: "Unhandled error encountered during call", data: String(describing: error)))
        }
    }

    func didReceiveRequest(_ json: [String: Any], completion: @escaping JSONRPCCompletionHandler) throws {
        guard let method = json["method"] as? String else {
            throw JSONRPCError.invalidRequest(data: "method value missing or not a string")
        }

        // optional: dictionary of parameters by name
        let params: [String: Any] = (json["params"] as? [String: Any]) ?? [String: Any]()

        // On success, this will call makeResponse with a result
        try didReceiveCall(method, withParams: params, completion: completion)
    }

    func didReceiveResponse(_ json: [String: Any]) throws {
        guard let requestId = json["id"] as? RequestID else {
            throw JSONRPCError.invalidRequest(data: "response ID value missing or wrong type")
        }

        guard let completionHandler = completionHandlers.removeValue(forKey: requestId) else {
            throw JSONRPCError.invalidRequest(data: "response ID does not correspond to any open request")
        }

        if let errorJSON = json["error"] as? [String: Any] {
            let error = JSONRPCError(fromJSON: errorJSON)
            completionHandler(nil, error)
        } else {
            let rawResult = json["result"]
            let result = (rawResult is NSNull ? nil : rawResult)
            completionHandler(result, nil)
        }
    }

    private func getNextId() -> RequestID {
        let result = self.nextId
        self.nextId += 1
        return result
    }
}
