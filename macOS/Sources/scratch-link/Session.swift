import Foundation
import PerfectHTTP
import PerfectWebSockets

enum SessionError: Error {
    case mutexInit(Int32)
}

// TODO: implement remaining JSON-RPC 2.0 features like batching
class Session {
    typealias RequestID = Int
    typealias JSONRPCCompletionHandler = (_ result: Any?, _ error: JSONRPCError?) -> Void

    let socketProtocol: String? = nil // must match client sub-protocol
    private let webSocket: WebSocket
    private var nextId: RequestID
    private var completionHandlers: [RequestID: JSONRPCCompletionHandler]

    // Mutex for the WebSocket
    private var socketMutex = pthread_mutex_t()

    // Mutex for the derived session (didReceiveCall and completion handlers)
    private var sessionMutex = pthread_mutex_t()

    required init(withSocket webSocket: WebSocket) throws {
        self.webSocket = webSocket
        self.nextId = 0
        self.completionHandlers = [RequestID: JSONRPCCompletionHandler]()

        let sessionMutexInit = pthread_mutex_init(&sessionMutex, nil)
        if sessionMutexInit != 0 {
            throw SessionError.mutexInit(sessionMutexInit)
        }

        let socketMutexInit = pthread_mutex_init(&socketMutex, nil)
        if socketMutexInit != 0 {
            throw SessionError.mutexInit(socketMutexInit)
        }
    }

    func usingMutex<T>(_ mutex: UnsafeMutablePointer<pthread_mutex_t>, _ task: () throws -> T) rethrows -> T {
        let resultCode = pthread_mutex_lock(mutex)
        if resultCode != 0 {
            fatalError("Could not obtain session lock: resultCode = \(resultCode)")
        }
        defer { pthread_mutex_unlock(mutex) }
        return try task()
    }

    func handleSession(request req: HTTPRequest, socket: WebSocket) {
        var message = ""
        // Perfect will automatically convert binary messages to look like text messages
        // TODO: consider inspecting `op` for text/binary so we can send a matched response
        socket.readStringMessage { text, _, isFinal in
            guard let text = text else {
                // This block will be executed if, for example, the browser window is closed.
                self.sessionWasClosed()
                return
            }
            message.append(contentsOf: text)
            if isFinal {
                self.didReceiveText(message)
                message.removeAll()
            }
            // "recurse" to continue the session
            self.handleSession(request: req, socket: socket)
        }
    }

    // Override this to clean up session-specific resources, if any.
    func sessionWasClosed() {
        usingMutex(&sessionMutex) {
            if completionHandlers.count > 0 {
                print("Warning: session was closed with \(completionHandlers.count) pending requests")
                for (_, completionHandler) in completionHandlers {
                    completionHandler(nil, JSONRPCError.internalError(data: "Session closed"))
                }
            }
        }
    }

    func didReceiveText(_ text: String) {
        guard let messageData = text.data(using: .utf8) else {
            print("Failed to convert client text to UTF8")
            return
        }
        didReceiveData(messageData) { jsonResponseData in
            if let jsonResponseData = jsonResponseData {
                if let jsonResponseText = String(data: jsonResponseData, encoding: .utf8) {
                    self.usingMutex(&self.socketMutex) {
                        self.webSocket.sendStringMessage(string: jsonResponseText, final: true) {}
                    }
                } else {
                    print("Failed to decode response")
                }
            }
        }
    }

    // Override this to handle received RPC requests & notifications.
    // Call the completion handler when done with a request:
    // - pass your call's "return value" (or nil) as `result` on success
    // - pass an instance of `JSONRPCError` for `error` on failure
    // You may also throw a `JSONRPCError` (or any other `Error`) iff it is encountered synchronously.
    func didReceiveCall(_ method: String, withParams params: [String: Any],
                        completion: @escaping JSONRPCCompletionHandler) throws {
        preconditionFailure("Must override didReceiveCall")
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
            let requestId: RequestID = usingMutex(&sessionMutex) {
                let requestId = getNextId()
                completionHandlers[requestId] = completion
                return requestId
            }
            request["id"] = requestId
        }

        do {
            let requestData = try JSONSerialization.data(withJSONObject: request)
            guard let requestText = String(data: requestData, encoding: .utf8) else {
                throw SerializationError.internalError("Could not serialize request before sending to client")
            }
            self.usingMutex(&socketMutex) {
                self.webSocket.sendStringMessage(string: requestText, final: true) {}
            }
        } catch {
            print("Error serializing request JSON: \(error)")
            print("Request was: \(request)")
        }
    }

    func didReceiveData(_ data: Data, completion: @escaping (_ jsonResponseData: Data?) throws -> Void) {

        var responseId: Any = NSNull() // initialize with null until we try to read the real ID

        func makeResponse(_ result: Any?, _ error: JSONRPCError?) -> [String: Any] {
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

        func sendResponse(_ result: Any?, _ error: JSONRPCError?) {
            do {
                let response = makeResponse(result, error)
                let jsonData = try JSONSerialization.data(withJSONObject: response)
                try completion(jsonData)
            } catch let firstError {
                do {
                    let errorResponse = makeResponse(nil, JSONRPCError(
                            code: 2, message: "Could not encode response", data: String(describing: firstError)))
                    let jsonData = try JSONSerialization.data(withJSONObject: errorResponse)
                    try completion(jsonData)
                } catch let secondError {
                    print("Failure to report failure to encode!")
                    print("Initial error: \(String(describing: firstError))")
                    print("Secondary error: \(String(describing: secondError))")
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
        try usingMutex(&sessionMutex) {
            try didReceiveCall(method, withParams: params, completion: completion)
        }
    }

    func didReceiveResponse(_ json: [String: Any]) throws {
        guard let requestId = json["id"] as? RequestID else {
            throw JSONRPCError.invalidRequest(data: "response ID value missing or wrong type")
        }

        guard let completionHandler = (usingMutex(&sessionMutex) {
            return completionHandlers.removeValue(forKey: requestId)
        }) else {
            throw JSONRPCError.invalidRequest(data: "response ID does not correspond to any open request")
        }

        if let errorJSON = json["error"] as? [String: Any] {
            let error = JSONRPCError(fromJSON: errorJSON)
            usingMutex(&sessionMutex) {
                completionHandler(nil, error)
            }
        } else {
            let rawResult = json["result"]
            let result = (rawResult is NSNull ? nil : rawResult)
            usingMutex(&sessionMutex) {
                completionHandler(result, nil)
            }
        }
    }

    private func getNextId() -> RequestID {
        let result = self.nextId
        self.nextId += 1
        return result
    }
}
